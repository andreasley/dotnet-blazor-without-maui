using BlazorNative.Shared;
using BlazorNative.WebKit;

namespace BlazorNative.iOS;

/// <summary>Full-screen view controller that hosts the Blazor web view.</summary>
public sealed class BlazorViewController : UIViewController
{
    private readonly BlazorWebView _blazor;

    public BlazorViewController(IServiceProvider services)
    {
        _blazor = new BlazorWebView(services);
        _blazor.AddRootComponent<Routes>("#app");
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        View!.BackgroundColor = UIColor.SystemBackground;

        var webView = _blazor.WebView;
        webView.TranslatesAutoresizingMaskIntoConstraints = false;
        View.AddSubview(webView);

        NSLayoutConstraint.ActivateConstraints(
        [
            webView.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            webView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
            webView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            webView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
        ]);

        _blazor.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blazor.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}
