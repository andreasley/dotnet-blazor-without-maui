using System.Globalization;
using Foundation;
using Microsoft.Extensions.Logging;
using WebKit;

namespace BlazorNative.WebKit;

/// <summary>
/// WKURLSchemeHandler for the "app" scheme. WebKit calls this for every resource the page requests
/// from app://localhost/ and we answer from the WebViewManager, which acts as an in-process web server.
/// </summary>
internal sealed class BlazorSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    private readonly Func<WebKitWebViewManager?> _manager;
    private readonly ILogger _logger;

    public BlazorSchemeHandler(Func<WebKitWebViewManager?> manager, ILogger logger)
    {
        _manager = manager;
        _logger = logger;
    }

    [Export("webView:startURLSchemeTask:")]
    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var url = urlSchemeTask.Request.Url;
        if (url is null)
        {
            // -1000 is NSURLErrorBadURL.
            urlSchemeTask.DidFailWithError(new NSError(NSError.NSUrlErrorDomain, -1000));
            return;
        }

        try
        {
            var manager = _manager();
            if (manager is not null
                && manager.TryGetResponse(url.AbsoluteString!, out var statusCode, out _, out var content, out var headers))
            {
                byte[] body;
                using (content)
                using (var buffer = new MemoryStream())
                {
                    content.CopyTo(buffer);
                    body = buffer.ToArray();
                }

                var responseHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Length"] = body.Length.ToString(CultureInfo.InvariantCulture),
                };
                // Never let WebKit cache app content; stale JS would break the IPC protocol after an update.
                responseHeaders.TryAdd("Cache-Control", "no-cache, max-age=0, must-revalidate, no-store");

                _logger.LogDebug("{StatusCode} {Url} ({Length} bytes)", statusCode, url.AbsoluteString, body.Length);
                using var response = new NSHttpUrlResponse(url, statusCode, "HTTP/1.1", ToNSDictionary(responseHeaders));
                urlSchemeTask.DidReceiveResponse(response);
                urlSchemeTask.DidReceiveData(NSData.FromArray(body));
                urlSchemeTask.DidFinish();
            }
            else
            {
                _logger.LogWarning("404 for {Url}", url.AbsoluteString);
                using var response = new NSHttpUrlResponse(url, 404, "HTTP/1.1", null);
                urlSchemeTask.DidReceiveResponse(response);
                urlSchemeTask.DidFinish();
            }
        }
        catch (Exception ex)
        {
            // WebKit throws an Objective-C exception if the task was already stopped; log and move on.
            _logger.LogError(ex, "Failed to serve {Url}", url.AbsoluteString);
        }
    }

    [Export("webView:stopURLSchemeTask:")]
    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        // Responses are produced synchronously above, so there is nothing to cancel.
    }

    private static NSDictionary ToNSDictionary(IReadOnlyDictionary<string, string> values)
    {
        var keys = new NSObject[values.Count];
        var objects = new NSObject[values.Count];
        var i = 0;
        foreach (var (key, value) in values)
        {
            keys[i] = new NSString(key);
            objects[i] = new NSString(value);
            i++;
        }

        return NSDictionary.FromObjectsAndKeys(objects, keys);
    }
}
