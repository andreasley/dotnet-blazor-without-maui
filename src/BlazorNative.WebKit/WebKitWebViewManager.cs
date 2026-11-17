using System.Text.Encodings.Web;
using Foundation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using WebKit;

namespace BlazorNative.WebKit;

/// <summary>
/// The glue between Blazor's <see cref="WebViewManager"/> and a concrete <see cref="WKWebView"/>.
///
/// WebViewManager owns the Blazor renderer, the JS interop runtime and the navigation manager. It
/// talks to the browser through three primitives that a platform has to provide:
///   1. navigate the browser to a URL (<see cref="NavigateCore"/>),
///   2. push a text message into the page (<see cref="SendMessage"/>),
///   3. receive text messages from the page (<see cref="OnMessageReceived"/>),
/// plus serving files for the app's origin (<see cref="TryGetResponse"/>).
/// </summary>
internal sealed class WebKitWebViewManager : WebViewManager
{
    /// <summary>
    /// Custom scheme the app is served from. Using a non-HTTP origin means no sockets, no server,
    /// and the browser treats everything from this origin as same-origin.
    /// </summary>
    public const string AppScheme = "app";

    public const string AppHostAddress = "localhost";

    public static readonly Uri AppOriginUri = new($"{AppScheme}://{AppHostAddress}/");

    private readonly WKWebView _webView;
    private readonly ILogger _logger;

    public WebKitWebViewManager(
        WKWebView webView,
        IServiceProvider services,
        IFileProvider fileProvider,
        JSComponentConfigurationStore jsComponents,
        string hostPageRelativePath,
        ILogger logger)
        : base(services, new MainThreadDispatcher(), AppOriginUri, fileProvider, jsComponents, hostPageRelativePath)
    {
        _webView = webView;
        _logger = logger;
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        _logger.LogDebug("Navigating WKWebView to {Uri}", absoluteUri);
        using var url = new NSUrl(absoluteUri.ToString());
        using var request = new NSUrlRequest(url);
        _webView.LoadRequest(request);
    }

    protected override void SendMessage(string message)
    {
        // The page side registered window.__dispatchMessageCallback in the init script (see BlazorWebView).
        var literal = JavaScriptEncoder.Default.Encode(message);
        var script = $"__dispatchMessageCallback(\"{literal}\")";

        if (NSThread.IsMain)
        {
            Evaluate(script);
        }
        else
        {
            _webView.InvokeOnMainThread(() => Evaluate(script));
        }
    }

    private void Evaluate(string script)
    {
        _webView.EvaluateJavaScript(script, (_, error) =>
        {
            if (error is not null)
            {
                _logger.LogError("JavaScript dispatch failed: {Error}", error.LocalizedDescription);
            }
        });
    }

    /// <summary>Called by the WKScriptMessageHandler when the page posts a message.</summary>
    public void OnMessageReceived(string message) => MessageReceived(AppOriginUri, message);

    /// <summary>
    /// Resolves a request for the app origin: files from wwwroot, the embedded
    /// _framework/blazor.webview.js, or the host page as a fallback for client-side routes.
    /// </summary>
    public bool TryGetResponse(
        string url,
        out int statusCode,
        out string statusMessage,
        out Stream content,
        out IDictionary<string, string> headers)
    {
        var uri = new Uri(url);

        // Deep links such as app://localhost/counter have no file extension and must serve index.html
        // so that the Blazor router can take over. Requests with an extension are real files.
        var allowFallbackOnHostPage = AppOriginUri.IsBaseOf(uri) && !Path.HasExtension(uri.AbsolutePath);
        var withoutQuery = uri.GetLeftPart(UriPartial.Path);

        return TryGetResponseContent(withoutQuery, allowFallbackOnHostPage, out statusCode, out statusMessage, out content, out headers);
    }
}
