using BlazorNative.Shared.Services;
using CoreFoundation;

namespace BlazorNative.MacOS;

internal sealed class MacNativeDialogs : INativeDialogs
{
    public Task ShowAlertAsync(string title, string message)
    {
        var completion = new TaskCompletionSource();

        // Always hop asynchronously: the caller is a Blazor event handler on the main thread and
        // the renderer should finish its batch before a nested modal run loop starts.
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                using var alert = new NSAlert
                {
                    MessageText = title,
                    InformativeText = message,
                    AlertStyle = NSAlertStyle.Informational,
                };
                alert.AddButton("OK");
                alert.RunModal();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }
}
