using BlazorNative.Shared;
using BlazorNative.Shared.Services;
using BlazorNative.WebKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObjCRuntime;

namespace BlazorNative.MacOS;

[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private ServiceProvider? _services;
    private BlazorWebView? _blazor;
    private NSWindow? _window;

    public override void DidFinishLaunching(NSNotification notification)
    {
        InstallMainMenu();

        _services = BuildServices();

        _blazor = new BlazorWebView(_services);
        _blazor.AddRootComponent<Routes>("#app");

        _window = new NSWindow(
            new CGRect(0, 0, 1000, 700),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            deferCreation: false)
        {
            Title = "Blazor Native (macOS)",
            ContentView = _blazor.WebView,
            MinSize = new CGSize(480, 320),
        };
        _window.Center();
        _window.MakeKeyAndOrderFront(this);
        NSApplication.SharedApplication.Activate();

        _blazor.Start();
    }

    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) => true;

    public override void WillTerminate(NSNotification notification)
    {
        _blazor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _services?.Dispose();
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
        services.AddSingleton<IPlatformInfo, MacPlatformInfo>();
        services.AddSingleton<INativeDialogs, MacNativeDialogs>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A programmatic main menu so that ⌘Q quits and ⌘X/⌘C/⌘V/⌘A reach the web view.
    /// </summary>
    private static void InstallMainMenu()
    {
        var mainMenu = new NSMenu();

        var appMenuItem = new NSMenuItem();
        var appMenu = new NSMenu();
        appMenu.AddItem(new NSMenuItem("Quit Blazor Native", new Selector("terminate:"), "q"));
        appMenuItem.Submenu = appMenu;
        mainMenu.AddItem(appMenuItem);

        var editMenuItem = new NSMenuItem("Edit");
        var editMenu = new NSMenu("Edit");
        editMenu.AddItem(new NSMenuItem("Undo", new Selector("undo:"), "z"));
        editMenu.AddItem(new NSMenuItem("Redo", new Selector("redo:"), "Z"));
        editMenu.AddItem(NSMenuItem.SeparatorItem);
        editMenu.AddItem(new NSMenuItem("Cut", new Selector("cut:"), "x"));
        editMenu.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
        editMenu.AddItem(new NSMenuItem("Paste", new Selector("paste:"), "v"));
        editMenu.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
        editMenuItem.Submenu = editMenu;
        mainMenu.AddItem(editMenuItem);

        NSApplication.SharedApplication.MainMenu = mainMenu;
    }
}
