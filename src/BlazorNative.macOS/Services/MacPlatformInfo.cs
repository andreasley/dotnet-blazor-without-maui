using System.Runtime.InteropServices;
using BlazorNative.Shared.Services;

namespace BlazorNative.MacOS;

internal sealed class MacPlatformInfo : IPlatformInfo
{
    public string PlatformName => "macOS (AppKit)";

    public string OperatingSystemVersion => NSProcessInfo.ProcessInfo.OperatingSystemVersionString;

    public string DeviceDescription => $"{Environment.MachineName} ({RuntimeInformation.OSArchitecture})";

    public string RuntimeDescription => RuntimeInformation.FrameworkDescription;
}
