namespace BlazorNative.Shared.Services;

/// <summary>
/// Native UI that Blazor components can trigger. Each host implements this with its platform toolkit.
/// </summary>
public interface INativeDialogs
{
    /// <summary>Shows a modal alert and completes once the user dismissed it.</summary>
    Task ShowAlertAsync(string title, string message);
}
