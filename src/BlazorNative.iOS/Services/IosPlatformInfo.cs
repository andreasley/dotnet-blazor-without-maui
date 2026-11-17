using System.Runtime.InteropServices;
using BlazorNative.Shared.Services;

namespace BlazorNative.iOS;

internal sealed class IosPlatformInfo : IPlatformInfo
{
    public string PlatformName => "iOS (UIKit)";

    public string OperatingSystemVersion => $"{UIDevice.CurrentDevice.SystemName} {UIDevice.CurrentDevice.SystemVersion}";

    public string DeviceDescription => $"{UIDevice.CurrentDevice.Model} ({RuntimeInformation.OSArchitecture})";

    public string RuntimeDescription => RuntimeInformation.FrameworkDescription;
}
