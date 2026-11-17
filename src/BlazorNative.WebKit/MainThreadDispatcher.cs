using CoreFoundation;
using Foundation;
using Microsoft.AspNetCore.Components;

namespace BlazorNative.WebKit;

/// <summary>
/// Blazor <see cref="Dispatcher"/> that runs all renderer work on the Apple main thread.
/// The renderer requires every component operation to happen on a single logical thread, and
/// WKWebView may only be touched from the main thread, so using the main thread for both
/// keeps things simple. (This mirrors what MAUI's dispatcher does for BlazorWebView.)
/// </summary>
internal sealed class MainThreadDispatcher : Dispatcher
{
    public override bool CheckAccess() => NSThread.IsMain;

    public override Task InvokeAsync(Action workItem)
    {
        if (CheckAccess())
        {
            workItem();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                workItem();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public override Task InvokeAsync(Func<Task> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try
            {
                await workItem().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
    {
        if (CheckAccess())
        {
            return Task.FromResult(workItem());
        }

        var tcs = new TaskCompletionSource<TResult>();
        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            try
            {
                tcs.SetResult(workItem());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
    {
        if (CheckAccess())
        {
            return workItem();
        }

        var tcs = new TaskCompletionSource<TResult>();
        DispatchQueue.MainQueue.DispatchAsync(async () =>
        {
            try
            {
                tcs.SetResult(await workItem().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
