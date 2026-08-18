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
        var activatedEventArgs = currentInstance.GetActivatedEventArgs();
        var mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);

        if (mainInstance.IsCurrent)
        {
            return false;
        }

        mainInstance.RedirectActivationToAsync(activatedEventArgs).AsTask().GetAwaiter().GetResult();
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
