using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;

namespace EmulatorPCHub.App.Views;

/// <summary>Power-Menü (Plan Abschnitt 20): Sleep, Restart, Shutdown, Exit to Windows, Desktop sperren.</summary>
public static class PowerMenu
{
    public static async Task ShowAsync()
    {
        string? choice = null;
        var panel = new StackPanel { Spacing = 8, MinWidth = 360 };
        ContentDialog? dialog = null;
        void Add(string glyph, string text, string id)
        {
            var b = Ui.Action(text, glyph, () =>
            {
                choice = id;
                Dialogs.Current?.Hide();
            });
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Margin = new Thickness(0);
            panel.Children.Add(b);
        }
        Add("", "Energie sparen (Sleep)", "sleep");
        Add("", "Neu starten", "restart");
        Add("", "Herunterfahren", "shutdown");
        Add("", "Windows-Desktop sperren", "lock");
        Add("", "Zu Windows zurückkehren (Hub beenden)", "exit");
        _ = dialog;

        await Dialogs.ShowAsync("Power", panel, null, "Abbrechen");
        switch (choice)
        {
            case "sleep":
                SystemService.Sleep();
                break;
            case "restart":
                if (await Dialogs.ConfirmAsync("Neu starten", "Den PC jetzt neu starten?", "Neu starten"))
                    SystemService.Restart();
                break;
            case "shutdown":
                if (await Dialogs.ConfirmAsync("Herunterfahren", "Den PC jetzt herunterfahren?", "Herunterfahren"))
                    SystemService.Shutdown();
                break;
            case "lock":
                SystemService.Lock();
                break;
            case "exit":
                Application.Current.Exit();
                break;
        }
    }
}
