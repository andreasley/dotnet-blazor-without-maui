using BlazorNative.Shared.Services;
using CoreFoundation;

namespace BlazorNative.iOS;

internal sealed class IosNativeDialogs : INativeDialogs
{
    public Task ShowAlertAsync(string title, string message)
    {
        var completion = new TaskCompletionSource();

        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            var presenter = FindTopViewController();
            if (presenter is null)
            {
                completion.SetResult();
                return;
            }

            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => completion.TrySetResult()));
            presenter.PresentViewController(alert, animated: true, completionHandler: null);
        });

        return completion.Task;
    }

    private static UIViewController? FindTopViewController()
    {
        var scene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .OrderByDescending(s => s.ActivationState == UISceneActivationState.ForegroundActive)
            .FirstOrDefault();

        var controller = scene?.KeyWindow?.RootViewController;
        while (controller?.PresentedViewController is { } presented)
        {
            controller = presented;
        }

        return controller;
    }
}
