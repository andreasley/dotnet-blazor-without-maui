using BlazorNative.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorNative.iOS;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    /// <summary>Application-wide service provider, shared by all scenes.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        Services = BuildServices();
        return true;
    }

    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
    {
        // "Default Configuration" is declared in Info.plist and points to SceneDelegate.
        return new UISceneConfiguration("Default Configuration", connectingSceneSession.Role);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.AddSimpleConsole(options => options.SingleLine = true);
#if DEBUG
            logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Information);
#endif
        });

        // Registers the scoped Blazor services (NavigationManager, IJSRuntime, ...) that WebViewManager needs.
        services.AddBlazorWebView();

        // Native services that the shared Razor components consume.
        services.AddSingleton<IPlatformInfo, IosPlatformInfo>();
        services.AddSingleton<INativeDialogs, IosNativeDialogs>();

        return services.BuildServiceProvider();
    }
}
