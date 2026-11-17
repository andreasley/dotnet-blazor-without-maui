Non-obvious facts learned while hosting Blazor in WKWebView without MAUI (.NET 11, Xcode 27.2)
---

Findings from building the Blazor-in-WKWebView host (2026-11-17):

- Current WebKit rejects `app://0.0.0.0/` ("Not allowed to use restricted network port"); use `app://localhost/`.
- `Microsoft.AspNetCore.Components.WebView` 11.0.0 ships `blazor.webview.js` as a *static web asset*, not as an embedded resource. Apps must consume the Static Web Assets pipeline (Razor SDK + a target that converts `@(StaticWebAsset)` into `BundleResource` before `_CollectBundleResources`, see `src/BlazorNative.WebKit/build`).
- The Static Web Assets SDK adds `**/*.json` as Content at evaluation time, which breaks asset catalogs on Apple platforms; remove `**/*.xcassets/**` from Content at evaluation time.
- `ApplicationTitle` in the csproj renames the `.app` bundle (spaces included); use `CFBundleDisplayName` instead.
- Universal macOS builds from the CLI need `-p:RuntimeIdentifiers=\"osx-arm64\;osx-x64\"`.
- The macOS SDK ad-hoc signs by default.

**Why:** each of these cost a build/debug cycle; they are not documented in one place. **How to apply:** keep these in mind when extending the project or upgrading .NET/Xcode.
