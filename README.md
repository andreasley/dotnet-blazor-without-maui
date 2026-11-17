# Blazor Native: Blazor Hybrid on macOS and iOS without .NET MAUI

A .NET 11 solution that produces a **native macOS app (AppKit)** and a **native iOS app (UIKit)** whose entire
user interface is **Blazor** rendered inside a `WKWebView`. The Razor components run in-process in the .NET
runtime of the app, exactly like Blazor Hybrid, but no MAUI assembly is referenced anywhere.

The piece MAUI normally contributes, the `BlazorWebView` control, is replaced by a ~400 line library
(`src/BlazorNative.WebKit`) built directly on the public engine behind every BlazorWebView
(`Microsoft.AspNetCore.Components.WebView`) and on the .NET for iOS / .NET for macOS WebKit bindings.

```
┌────────────────────────────┐   ┌──────────────────────────────┐
│ BlazorNative.macOS         │   │ BlazorNative.iOS             │   native shells
│ net11.0-macos, NSWindow    │   │ net11.0-ios, UIViewController│
└──────────────┬─────────────┘   └──────────────┬───────────────┘
               │ BlazorWebView (WKWebView)      │
┌──────────────┴────────────────────────────────┴─────────────┐
│ BlazorNative.WebKit  net11.0-ios;net11.0-macos              │   Blazor <-> WebKit bridge
│   WebKitWebViewManager: WebViewManager (Blazor renderer)    │
│   BlazorSchemeHandler : WKURLSchemeHandler (app://localhost)│
│   BlazorScriptMessageHandler, MainThreadDispatcher          │
│   build/BlazorNative.WebKit.targets (static web assets)     │
└──────────────┬──────────────────────────────────────────────┘
┌──────────────┴──────────────────────────────────────────────┐
│ BlazorNative.Shared  net11.0 Razor Class Library            │   the UI
│   Routes, MainLayout, Pages/Home, Pages/Counter, wwwroot/css│
│   IPlatformInfo, INativeDialogs (implemented by each shell) │
└─────────────────────────────────────────────────────────────┘
```

## How it works

1. **`WebViewManager`** (from `Microsoft.AspNetCore.Components.WebView`) owns the Blazor renderer, JS interop
   and navigation. `WebKitWebViewManager` implements its three platform hooks: navigate the browser, push a
   message into the page (`EvaluateJavaScript`), and receive messages from the page.
2. **`app://localhost/`** is the app origin. A `WKURLSchemeHandler` answers every request from the
   `WebViewManager`, which serves `wwwroot/` out of the app bundle plus `_framework/blazor.webview.js`.
   There is no socket and no web server. (`0.0.0.0`, the origin MAUI used for years, is rejected by current
   WebKit with "Not allowed to use restricted network port".)
3. **IPC** uses `window.webkit.messageHandlers.webwindowinterop.postMessage` (page to .NET) and
   `__dispatchMessageCallback` (.NET to page), wired up by a `WKUserScript` injected after the page loads.
4. **Static web assets**: since .NET 8 the `blazor.webview.js` script ships as a *static web asset*, not an
   embedded resource. `build/BlazorNative.WebKit.targets` hooks the Static Web Assets pipeline and turns the
   resolved assets (app `wwwroot/`, `_content/<RCL>/`, `_framework/*`) into `BundleResource` items. This is
   what MAUI's `ConvertStaticWebAssetsToMauiAssets` target does. The app projects therefore use the
   `Microsoft.NET.Sdk.Razor` SDK, the same as MAUI Blazor apps.
5. **Native interop** is plain dependency injection: the shells register `IPlatformInfo` and `INativeDialogs`
   (`NSAlert` / `UIAlertController`) and the shared Razor components inject them.

## Prerequisites (as installed on the build VM)

| Component | Version |
|-----------|---------|
| macOS | 27.2 |
| Xcode (selected with `xcode-select`) | 27.2 |
| .NET SDK | 11.0.100 |
| Workloads | `ios`, `macos` (version 27.2.11039) |

```sh
sudo xcode-select -s /Applications/Xcode.app
sudo dotnet workload install ios macos
dotnet workload list
```

## Repository layout

```
BlazorNative.slnx
Directory.Build.props            shared settings and package versions
src/BlazorNative.Shared/         Razor Class Library (components, css, service interfaces)
src/BlazorNative.WebKit/         WKWebView host for Blazor + MSBuild targets
src/BlazorNative.macOS/          AppKit app (Main.cs, AppDelegate.cs, Info.plist, Entitlements.plist, wwwroot/index.html)
src/BlazorNative.iOS/            UIKit app (AppDelegate.cs, SceneDelegate.cs, BlazorViewController.cs, Info.plist, wwwroot/index.html)
scripts/run-macos.sh             build + run the macOS app from a terminal
scripts/run-ios-simulator.sh     build + install + launch on an iPhone simulator with console output
```

## Building

All commands run from the repository root. Output paths are relative to the respective project.

### macOS

```sh
# Debug build (ad-hoc signed by the SDK, runs locally)
dotnet build src/BlazorNative.macOS/BlazorNative.macOS.csproj -c Debug
open src/BlazorNative.macOS/bin/Debug/net11.0-macos/osx-arm64/BlazorNative.app
#   or, with console logging:
scripts/run-macos.sh

# Release, single architecture
dotnet publish src/BlazorNative.macOS/BlazorNative.macOS.csproj -c Release -r osx-arm64
#   -> bin/Release/net11.0-macos/osx-arm64/BlazorNative.app

# Release, universal (arm64 + x86_64) app bundle and an installer .pkg
#   The semicolon has to reach MSBuild unsplit, hence the escaped quotes.
dotnet publish src/BlazorNative.macOS/BlazorNative.macOS.csproj -c Release \
    -p:RuntimeIdentifiers=\"osx-arm64\;osx-x64\" -p:CreatePackage=true
#   -> bin/Release/net11.0-macos/BlazorNative.app            (universal)
#   -> bin/Release/net11.0-macos/publish/BlazorNative-1.0.pkg
```

### iOS

```sh
# Simulator (no signing required)
dotnet build src/BlazorNative.iOS/BlazorNative.iOS.csproj -c Debug -r iossimulator-arm64
#   -> bin/Debug/net11.0-ios/iossimulator-arm64/BlazorNative.app
scripts/run-ios-simulator.sh          # boots an iPhone simulator, installs, launches with console output

# Device, unsigned (compiles and links; the .app cannot be installed without a signature)
dotnet build src/BlazorNative.iOS/BlazorNative.iOS.csproj -c Release -r ios-arm64 -p:EnableCodeSigning=false
#   -> bin/Release/net11.0-ios/ios-arm64/BlazorNative.app
```

Device and distribution builds need a signing identity and provisioning profile, see below.

## Signing

Signing is driven by MSBuild properties of the .NET for iOS/macOS SDK. They can be passed on the command
line (as shown) or put into the `.csproj` inside a `Condition="'$(Configuration)' == 'Release'"` property group.

| Property | Meaning |
|----------|---------|
| `EnableCodeSigning` | Sign the bundle (`true`/`false`). Defaults to `true`; macOS Debug builds sign ad hoc. |
| `CodesignKey` | Name of the identity in the keychain, e.g. `Developer ID Application: Name (TEAMID)`. `-` means ad hoc. |
| `CodesignProvision` | Name or UUID of the provisioning profile (iOS devices, App Store, Developer ID on macOS). |
| `CodesignEntitlements` | Entitlements file. `Entitlements.plist` in the project directory is picked up automatically. |
| `UseHardenedRuntime` | macOS: enable the hardened runtime (required for notarization). |
| `CreatePackage` | macOS: also build an installer `.pkg` (`bin/Release/net11.0-macos/publish/BlazorNative-1.0.pkg`). |
| `EnablePackageSigning` / `PackageSigningKey` | macOS: sign the `.pkg` with `Developer ID Installer: ...` (or `3rd Party Mac Developer Installer` for the App Store). |
| `ArchiveOnBuild` | iOS: additionally produce an `.ipa` (`bin/Release/net11.0-ios/ios-arm64/publish/BlazorNative.ipa`). |

List the identities available in the keychain with:

```sh
security find-identity -v -p codesigning
```

### macOS, distribution outside the App Store (Developer ID + notarization)

```sh
dotnet publish src/BlazorNative.macOS/BlazorNative.macOS.csproj -c Release \
    -p:RuntimeIdentifiers=\"osx-arm64\;osx-x64\" \
    -p:EnableCodeSigning=true \
    -p:CodesignKey="Developer ID Application: YOURTEAM (TEAMID)" \
    -p:UseHardenedRuntime=true \
    -p:CodesignEntitlements=Entitlements.plist \
    -p:CreatePackage=true \
    -p:EnablePackageSigning=true \
    -p:PackageSigningKey="Developer ID Installer: YOURTEAM (TEAMID)"

# Notarize and staple (one-time: xcrun notarytool store-credentials "AC_PROFILE" --apple-id ... --team-id ...)
xcrun notarytool submit src/BlazorNative.macOS/bin/Release/net11.0-macos/publish/BlazorNative-1.0.pkg \
    --keychain-profile "AC_PROFILE" --wait
xcrun stapler staple src/BlazorNative.macOS/bin/Release/net11.0-macos/publish/BlazorNative-1.0.pkg
```

`Entitlements.plist` of the macOS project already contains the entitlements the .NET runtime needs under the
hardened runtime (`allow-jit`, `allow-unsigned-executable-memory`, `disable-library-validation`).

Verify a signed bundle:

```sh
codesign --verify --deep --strict --verbose=2 src/BlazorNative.macOS/bin/Release/net11.0-macos/BlazorNative.app
spctl --assess --type execute --verbose src/BlazorNative.macOS/bin/Release/net11.0-macos/BlazorNative.app
```

### iOS, development device

```sh
dotnet build src/BlazorNative.iOS/BlazorNative.iOS.csproj -c Debug -r ios-arm64 \
    -p:CodesignKey="Apple Development: YOURTEAM (TEAMID)" \
    -p:CodesignProvision="BlazorNative Development"
# install on a connected device
xcrun devicectl device install app --device <DEVICE-ID> src/BlazorNative.iOS/bin/Debug/net11.0-ios/ios-arm64/BlazorNative.app
```

### iOS, App Store / ad-hoc distribution (.ipa)

```sh
dotnet publish src/BlazorNative.iOS/BlazorNative.iOS.csproj -c Release -r ios-arm64 \
    -p:ArchiveOnBuild=true \
    -p:CodesignKey="Apple Distribution: YOURTEAM (TEAMID)" \
    -p:CodesignProvision="BlazorNative App Store"
#   -> bin/Release/net11.0-ios/ios-arm64/publish/BlazorNative.ipa
xcrun altool --validate-app -f <ipa> -t ios --apiKey <KEY> --apiIssuer <ISSUER>   # or upload with Transporter
```

The bundle identifier (`com.yourcompany.blazornative`) is set through `ApplicationId` in the `.csproj` files and
must match the provisioning profile.

## What was verified on the VM

| Step | Result |
|------|--------|
| `dotnet build` macOS Debug, launch, Blazor renders | OK, log shows `Home page rendered on macOS (AppKit)` |
| `dotnet publish` macOS Release universal + `.pkg` | OK, fat arm64/x86_64 binary, `Signature=adhoc`, `codesign --verify` passes, app renders |
| `dotnet build` iOS simulator, install on iPhone 17 simulator, launch | OK, log shows `Home page rendered on iOS (UIKit)`, screenshot shows the UI |
| `dotnet build` iOS device Release with `EnableCodeSigning=false` | OK, arm64 app bundle produced |
| Signing with Developer ID / Apple Distribution certificates | **not run**: the VM has no signing identities (`security find-identity` lists none) |

## Notes and limitations

- Trimming is left at the SDK defaults (macOS: none, iOS Release: SDK assemblies only). Blazor's router finds
  pages by reflection, so when enabling `TrimMode=full` add `<TrimmerRootAssembly Include="BlazorNative.Shared" />`.
- Debug builds set `WKWebView.Inspectable`, so the page can be debugged with Safari > Develop.
- Hot reload of Razor components is not wired up (MAUI adds that through its own tooling).
- Logging goes to stdout (`Microsoft.Extensions.Logging.Console`). Start the app from a terminal or via
  `open --stdout <file> --stderr <file> <app>` to see it; on the simulator `simctl launch --console-pty` shows it.
