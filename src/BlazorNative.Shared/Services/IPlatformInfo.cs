namespace BlazorNative.Shared.Services;

/// <summary>
/// Information about the native host. Implemented separately by the macOS and iOS apps
/// and injected into Blazor components through dependency injection.
/// </summary>
public interface IPlatformInfo
{
    string PlatformName { get; }

    string OperatingSystemVersion { get; }

    string DeviceDescription { get; }

    string RuntimeDescription { get; }
}
