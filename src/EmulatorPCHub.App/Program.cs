using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace EmulatorPCHub.App;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        App.Arguments = args;

        // Nur eine Instanz: ein zweiter Start holt den laufenden Hub in den Vordergrund.
        if (RedirectToExistingInstance())
            return 0;

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }

    private static bool RedirectToExistingInstance()
    {
        try
        {
            var main = AppInstance.FindOrRegisterForKey("EmulatorPCHub.Main");
            if (main.IsCurrent)
            {
                main.Activated += (_, _) => App.OnRedirectedActivation();
                return false;
            }
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait(TimeSpan.FromSeconds(5));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
