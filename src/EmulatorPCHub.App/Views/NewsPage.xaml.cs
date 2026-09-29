using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI;

namespace EmulatorPCHub.App.Views;

/// <summary>News: lokaler Überblick – was ist bereit, was fehlt noch, Spielstatistiken.</summary>
public sealed partial class NewsPage : Page, IHubPage
{
    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück";

    public NewsPage()
    {
        InitializeComponent();
    }

    public void OnShown()
    {
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("News"));

        // Mario Kart – Bereitschaft
        Body.Children.Add(Ui.Header("Mario Kart – bereit zum Spielen?"));
        var mkwii = App.Hub.Library.Find(KnownGames.MarioKartWiiId)!;
        var rr = App.Hub.RetroRewind.LocalStatus();
        var wc = App.Hub.Adapters.WiiCompiled.FindInstall();
        var dolphin = App.Hub.Adapters.Dolphin.DetectInstallation();
        Body.Children.Add(Ui.Card(
            Ui.Text("Mario Kart Wii", 18, bold: true),
            Ui.Status(mkwii.IsPlaceholder ? "Eigener Dump fehlt noch (ISO/WBFS/RVZ)" : "Dump: " + Path.GetFileName(mkwii.Path), !mkwii.IsPlaceholder),
            Ui.Status(rr.Installed ? $"Retro Rewind {rr.Version} bereit" : "Retro Rewind fehlt", rr.Installed),
            Ui.Status(wc != null ? $"WiiCompiled {wc.Version} bereit" : "WiiCompiled noch nicht gebaut (braucht PAL-Dump)", wc != null),
            Ui.Status(dolphin.IsInstalled ? $"Dolphin {dolphin.Version} bereit (Fallback)" : "Dolphin fehlt", dolphin.IsInstalled),
            Ui.Buttons(Ui.Action("Zu Mario Kart Wii", "", () => MainWindow.Current.Navigate(typeof(MarioKartWiiPage))))));

        var mk8 = App.Hub.Library.Find(KnownGames.MarioKart8DeluxeId)!;
        var sw = App.Hub.Adapters.Switch;
        var swInst = sw.DetectInstallation();
        var sys = sw.GetSystemStatus();
        var ctgp = App.Hub.MarioKart8.CtgpStatus();
        var addons = App.Hub.MarioKart8.AddOnStatus();
        Body.Children.Add(Ui.Card(
            Ui.Text("Mario Kart 8 Deluxe", 18, bold: true),
            Ui.Status(mk8.IsPlaceholder ? "Eigener Dump fehlt noch (NSP/XCI)" : "Dump: " + Path.GetFileName(mk8.Path), !mk8.IsPlaceholder),
            Ui.Status(swInst.IsInstalled ? $"{sw.DisplayName} {swInst.Version}" : $"{sw.DisplayName} fehlt (Komponenten)", swInst.IsInstalled),
            Ui.Status(sys.KeysPresent ? "Keys vorhanden" : "Eigene Keys (prod.keys) fehlen", sys.KeysPresent),
            Ui.Status(sys.FirmwarePresent ? "Firmware installiert" : "Eigene Firmware fehlt", sys.FirmwarePresent),
            Ui.Status(ctgp.Installed ? $"CTGP Deluxe {ctgp.Version} bereit" : "CTGP Deluxe fehlt", ctgp.Installed),
            Ui.Status($"DLC-Dateien: {addons.DlcFilesFound} · Update: {addons.UpdateVersionText ?? "keins"}", addons.DlcFilesFound > 0),
            Ui.Buttons(Ui.Action("Zu Mario Kart 8 Deluxe", "", () => MainWindow.Current.Navigate(typeof(MarioKart8DeluxePage))))));

        // Statistiken
        Body.Children.Add(Ui.Header("Deine Statistik"));
        var games = App.Hub.Library.Games.ToList();
        var total = games.Sum(g => g.PlayTimeSeconds);
        var top = games.Where(g => g.PlayTimeSeconds > 0).OrderByDescending(g => g.PlayTimeSeconds).Take(5).ToList();
        var stats = new List<Microsoft.UI.Xaml.UIElement>
        {
            Ui.Text($"Gesamte Spielzeit: {Format.PlayTime(total)}", 17, bold: true),
            Ui.Subtle($"{games.Count(g => !g.IsPlaceholder)} Spiele in der Bibliothek · {games.Count(g => g.IsFavorite)} Favoriten"),
        };
        foreach (var g in top)
            stats.Add(Ui.Text($"{g.DisplayTitle}: {Format.PlayTime(g.PlayTimeSeconds)}"));
        Body.Children.Add(Ui.Card([.. stats]));

        Body.Children.Add(Ui.Header("Tipps"));
        Body.Children.Add(Ui.Card(
            Ui.Text("• F11 oder das Vollbild-Symbol oben rechts schaltet den Console Mode um."),
            Ui.Text("• Home + Minus 1,5 s halten beendet ein laufendes Spiel und bringt dich zurück in den Hub."),
            Ui.Text("• Unter Komponenten siehst du, ob es neue Versionen von Dolphin, Cemu, WiiCompiled, Retro Rewind & Co. gibt.")));
    }

    public bool HandleNav(NavAction action) => false;
}
