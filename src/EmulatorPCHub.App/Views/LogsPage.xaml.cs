using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.App.Views;

/// <summary>Settings → Troubleshooting → View Logs (Plan Abschnitt 28).</summary>
public sealed partial class LogsPage : Page, IHubPage
{
    public string Hints => "Ⓑ Zurück";

    public LogsPage()
    {
        InitializeComponent();
    }

    public void OnShown()
    {
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Logs"));
        Body.Children.Add(Ui.Buttons(Ui.Action("Log-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Paths.Logs))));

        Body.Children.Add(Ui.Header("Spielstarts"));
        var launches = App.Hub.LaunchLog.ReadRecent(40);
        if (launches.Count == 0)
            Body.Children.Add(Ui.Subtle("Noch keine Spielstarts."));
        foreach (var l in launches)
        {
            Body.Children.Add(Ui.Card(
                Ui.Status($"{l.Time.LocalDateTime:g} · {l.Game} · {l.Preset ?? "Standard"} · {l.Emulator}", l.Succeeded),
                Ui.Subtle($"Laufzeit: {TimeSpan.FromSeconds(l.DurationSeconds):hh\\:mm\\:ss} · Exit-Code: {l.ExitCode?.ToString() ?? "–"}"),
                Ui.Subtle($"{l.Executable} {l.Arguments}"),
                l.Error != null ? Ui.Status(l.Error, false) : Ui.Subtle(string.Join(" → ", l.Steps))));
        }

        Body.Children.Add(Ui.Header("Hub-Log (aktuelle Sitzung)"));
        var text = new TextBlock
        {
            Text = string.Join("\n", HubLog.RecentLines.TakeLast(120)),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Foreground = Ui.SubtleBrush,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        Body.Children.Add(Ui.Card(text));
    }

    public bool HandleNav(NavAction action) => false;
}
