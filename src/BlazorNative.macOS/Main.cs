using BlazorNative.MacOS;

// Entry point. There is no storyboard: AppDelegate builds the window in code.
NSApplication.Init();
NSApplication.SharedApplication.Delegate = new AppDelegate();
NSApplication.Main(args);
