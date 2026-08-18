using System;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace GroupsOnTaskbar.App.Activation;

public static class ActivationCoordinator
{
    private const string MainInstanceKey = "GroupsOnTaskbar.Main";

    public static bool RedirectToMainInstance()
    {
        var currentInstance = AppInstance.GetCurrent();

        // Activation arguments must be captured before registering the key.
        var activatedEventArgs = currentInstance.GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);

        if (mainInstance.IsCurrent)
        {
            return false;
        }

        // Redirecting with null activation arguments fails inside combase with
        // E_POINTER (0x80004003), so guard before calling into it.
        if (activatedEventArgs is null)
        {
            return true;
        }

        try
        {
            mainInstance.RedirectActivationToAsync(activatedEventArgs).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // The registered instance may exit between lookup and redirect. Exiting
            // quietly keeps single-instance behavior instead of crashing.
        }

        return true;
    }

    public static void RegisterActivationHandler(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("A UI DispatcherQueue is required to register the activation handler.");

        AppInstance.GetCurrent().Activated += (_, _) =>
        {
            if (!dispatcherQueue.TryEnqueue(() => handler()))
            {
                handler();
            }
        };
    }
}
