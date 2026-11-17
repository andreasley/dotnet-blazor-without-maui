using Foundation;
using Microsoft.Extensions.Logging;
using WebKit;

namespace BlazorNative.WebKit;

/// <summary>
/// Keeps the web view inside the app origin and logs navigation progress. Links to other origins
/// (https://..., mailto:...) are handed to the operating system instead of navigating the host page away.
/// </summary>
internal sealed class BlazorNavigationDelegate : WKNavigationDelegate
{
    private readonly ILogger _logger;

    public BlazorNavigationDelegate(ILogger logger)
    {
        _logger = logger;
    }

    public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
    {
        var url = navigationAction.Request.Url;
        if (url is null || url.Scheme == WebKitWebViewManager.AppScheme)
        {
            decisionHandler(WKNavigationActionPolicy.Allow);
            return;
        }

        if (navigationAction.NavigationType == WKNavigationType.LinkActivated)
        {
            OpenExternally(url);
        }

        decisionHandler(WKNavigationActionPolicy.Cancel);
    }

    public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
        => _logger.LogDebug("WKWebView finished loading {Url}", webView.Url?.AbsoluteString);

    public override void DidFailNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        => _logger.LogError("WKWebView navigation failed: {Error}", error.LocalizedDescription);

    public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
        => _logger.LogError("WKWebView provisional navigation failed: {Error}", error.LocalizedDescription);

    public override void ContentProcessDidTerminate(WKWebView webView)
        => _logger.LogError("WKWebView content process terminated");

    private static void OpenExternally(NSUrl url)
    {
#if MACOS
        AppKit.NSWorkspace.SharedWorkspace.OpenUrl(url);
#elif IOS
        UIKit.UIApplication.SharedApplication.OpenUrl(url, new UIKit.UIApplicationOpenUrlOptions(), null);
#endif
    }
}
