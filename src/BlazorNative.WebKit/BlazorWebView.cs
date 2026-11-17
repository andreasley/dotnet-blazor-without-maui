using CoreGraphics;
using Foundation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WebKit;

namespace BlazorNative.WebKit;

/// <summary>
/// A WKWebView that hosts a Blazor application, the counterpart of MAUI's BlazorWebView control
/// without any MAUI dependency.
///
/// Usage:
/// <code>
/// var blazor = new BlazorWebView(serviceProvider);
/// blazor.AddRootComponent&lt;Routes&gt;("#app");
/// window.ContentView = blazor.WebView;   // or add to a UIViewController's view
/// blazor.Start();
/// </code>
/// </summary>
public sealed class BlazorWebView : IAsyncDisposable
{
    /// <summary>
    /// Injected into the page after it loaded. It provides the window.external bridge that
    /// blazor.webview.js expects and then starts Blazor (the host page uses autostart="false").
    /// </summary>
    private const string InitScript = """
        window.__receiveMessageCallbacks = [];
        window.__dispatchMessageCallback = function(message) {
            window.__receiveMessageCallbacks.forEach(function(callback) { callback(message); });
        };
        window.external = {
            sendMessage: function(message) {
                window.webkit.messageHandlers.webwindowinterop.postMessage(message);
            },
            receiveMessage: function(callback) {
                window.__receiveMessageCallbacks.push(callback);
            }
        };
        Blazor.start();
        """;

    private readonly List<(Type Type, string Selector, ParameterView Parameters)> _rootComponents = [];
    private readonly BlazorSchemeHandler _schemeHandler;
    private readonly BlazorScriptMessageHandler _messageHandler;
    private readonly BlazorNavigationDelegate _navigationDelegate;
    private readonly ILogger _logger;
    private WebKitWebViewManager? _manager;

    public BlazorWebView(IServiceProvider services, CGRect frame = default)
    {
        Services = services;
        _logger = services.GetService<ILoggerFactory>()?.CreateLogger<BlazorWebView>() ?? NullLogger<BlazorWebView>.Instance;

        _schemeHandler = new BlazorSchemeHandler(() => _manager, _logger);
        _messageHandler = new BlazorScriptMessageHandler(message => _manager?.OnMessageReceived(message));
        _navigationDelegate = new BlazorNavigationDelegate(_logger);

        var configuration = new WKWebViewConfiguration();
        configuration.SetUrlSchemeHandler(_schemeHandler, WebKitWebViewManager.AppScheme);
        configuration.UserContentController.AddScriptMessageHandler(_messageHandler, BlazorScriptMessageHandler.HandlerName);
        configuration.UserContentController.AddUserScript(
            new WKUserScript(new NSString(InitScript), WKUserScriptInjectionTime.AtDocumentEnd, isForMainFrameOnly: true));

        WebView = new WKWebView(frame, configuration)
        {
            NavigationDelegate = _navigationDelegate,
        };

#if DEBUG
        // Allows Safari > Develop > <device> to attach the Web Inspector to the web view.
        WebView.Inspectable = true;
#endif
    }

    /// <summary>The native web view to place in the view hierarchy.</summary>
    public WKWebView WebView { get; }

    /// <summary>Service provider that Blazor components resolve their dependencies from.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Host page, relative to the app bundle's resource directory.</summary>
    public string HostPage { get; init; } = "wwwroot/index.html";

    /// <summary>Route to show first.</summary>
    public string StartPath { get; init; } = "/";

    public bool IsStarted => _manager is not null;

    /// <summary>Registers a Blazor component to render into the element matched by <paramref name="selector"/>.</summary>
    public void AddRootComponent<TComponent>(string selector, IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Root components must be added before Start() is called.");
        }

        _rootComponents.Add((typeof(TComponent), selector, parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters)));
    }

    /// <summary>Creates the Blazor runtime and navigates the web view to the start page.</summary>
    public void Start()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Start() was already called.");
        }

        var resourcePath = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("The main bundle has no resource path.");
        var contentRoot = Path.GetFullPath(Path.Combine(resourcePath, Path.GetDirectoryName(HostPage) ?? string.Empty));
        var hostPageFile = Path.GetFileName(HostPage);

        if (!File.Exists(Path.Combine(contentRoot, hostPageFile)))
        {
            throw new FileNotFoundException(
                $"Host page '{HostPage}' was not found in the app bundle. Make sure wwwroot is included as BundleResource.",
                Path.Combine(contentRoot, hostPageFile));
        }

        _logger.LogInformation("Serving Blazor content from {ContentRoot}", contentRoot);

        var fileProvider = new PhysicalFileProvider(contentRoot);
        _manager = new WebKitWebViewManager(WebView, Services, fileProvider, new JSComponentConfigurationStore(), hostPageFile, _logger);

        foreach (var (type, selector, parameters) in _rootComponents)
        {
            // Completes synchronously because no page is attached yet; the components are
            // rendered once blazor.webview.js connects.
            _ = _manager.AddRootComponentAsync(type, selector, parameters);
        }

        _manager.Navigate(StartPath);
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager is not null)
        {
            await _manager.DisposeAsync().ConfigureAwait(false);
            _manager = null;
        }

        WebView.Configuration.UserContentController.RemoveScriptMessageHandler(BlazorScriptMessageHandler.HandlerName);
        WebView.Dispose();
    }
}
