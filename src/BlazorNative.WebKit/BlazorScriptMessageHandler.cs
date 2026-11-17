using Foundation;
using WebKit;

namespace BlazorNative.WebKit;

/// <summary>
/// Receives messages the page posts through window.webkit.messageHandlers.webwindowinterop.postMessage(...)
/// and forwards them to the WebViewManager. This is the page-to-.NET half of the Blazor IPC channel.
/// </summary>
internal sealed class BlazorScriptMessageHandler : NSObject, IWKScriptMessageHandler
{
    public const string HandlerName = "webwindowinterop";

    private readonly Action<string> _onMessage;

    public BlazorScriptMessageHandler(Action<string> onMessage)
    {
        _onMessage = onMessage;
    }

    [Export("userContentController:didReceiveScriptMessage:")]
    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        if (message.Body is NSString text)
        {
            _onMessage(text.ToString());
        }
    }
}
