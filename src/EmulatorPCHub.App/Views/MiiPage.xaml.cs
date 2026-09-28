using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Mii;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Mii Manager: Miis importieren (Dolphin/Wii-NAND, Cemu-Konten, Dateien), im Original-Editor der Konsole erstellen
/// (Mii-Kanal in Dolphin, Mii Maker in Cemu, Mii-Editor in Eden – jeweils mit eigenen System-Dumps),
/// exportieren und zuweisen: Hub-Profil, Wii (Dolphin), Wii U (Cemu-Konto), Switch (Eden-Benutzer).
/// </summary>
public sealed partial class MiiPage : Page, IHubPage
{
    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück";

    public MiiPage()
    {
        InitializeComponent();
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Mii Manager"));
        Body.Children.Add(Ui.Subtle("Deine Miis aus Wii und Wii U an einem Ort. Ein Mii auswählen → einem Profil oder einer Konsole zuweisen. " +
                                    "Miis werden im nativen Format gespeichert und unverändert übernommen."));
        Body.Children.Add(Ui.Buttons(
            Ui.AsyncAction("Neues Mii erstellen …", "", CreateAsync, primary: true),
            Ui.AsyncAction("Aus Dolphin (Wii) importieren", "", () => ImportAsync(() => App.Hub.Miis.ImportFromDolphin(), "Dolphin")),
            Ui.AsyncAction("Aus Cemu (Wii U) importieren", "", () => ImportAsync(() => App.Hub.Miis.ImportFromCemu(), "Cemu")),
            Ui.AsyncAction("Datei importieren …", "", ImportFileAsync)));

        var miis = App.Hub.Miis.All;
        Body.Children.Add(Ui.Header($"Meine Miis ({miis.Count})"));
        if (miis.Count == 0)
            Body.Children.Add(Ui.Subtle("Noch keine Miis. Importiere sie aus Dolphin/Cemu oder erstelle eins im Mii-Editor deiner Konsole."));
        foreach (var mii in miis.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase))
            Body.Children.Add(BuildMii(mii));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private UIElement BuildMii(HubMii mii)
    {
        var info = mii.Info;
        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(MiiBadge.Create(mii, 72));

        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(Ui.Text(mii.Name, 20, bold: true));
        if (info != null)
        {
            var birthday = info.BirthMonth is >= 1 and <= 12 && info.BirthDay is >= 1 and <= 31 ? $"{info.BirthDay}.{info.BirthMonth}." : "–";
            panel.Children.Add(Ui.Subtle($"{MiiCodec.PlatformName(info.Format)}   ·   {(info.IsGirl ? "weiblich" : "männlich")}   ·   " +
                                         $"Lieblingsfarbe {MiiCodec.FavoriteColorNames[Math.Clamp(info.FavoriteColor, 0, 11)]}   ·   " +
                                         $"Geburtstag {birthday}" + (info.Creator.Length > 0 ? $"   ·   erstellt von {info.Creator}" : "")));
        }
        var users = App.Hub.Profiles.All.Where(p => p.MiiId == mii.Id).Select(p => p.Name).ToList();
        panel.Children.Add(Ui.Subtle($"Quelle: {mii.Source}" + (users.Count > 0 ? $"   ·   Profil: {string.Join(", ", users)}" : "")));
        panel.Children.Add(Ui.Buttons(
            Ui.AsyncAction("Zuweisen …", "", () => AssignAsync(mii)),
            Ui.AsyncAction("Umbenennen", "", async () =>
            {
                var name = await Dialogs.InputAsync("Mii umbenennen", "Name (max. 10 Zeichen)", mii.Name, 10);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    App.Hub.Miis.Rename(mii, name);
                    Build(keepFocus: true);
                }
            }),
            Ui.AsyncAction("Exportieren …", "", async () =>
            {
                var folder = await Dialogs.PickFolderAsync();
                if (folder != null)
                    MainWindow.Current.ShowToast("Exportiert: " + Path.GetFileName(App.Hub.Miis.Export(mii, folder)));
            }),
            Ui.AsyncAction("Löschen", "", async () =>
            {
                if (await Dialogs.ConfirmAsync("Mii löschen", $"„{mii.Name}“ aus dem Hub löschen? In den Konsolen bleibt es erhalten.", "Löschen"))
                {
                    foreach (var p in App.Hub.Profiles.All.Where(p => p.MiiId == mii.Id))
                    {
                        p.MiiId = null;
                        App.Hub.Profiles.Update(p);
                    }
                    App.Hub.Miis.Remove(mii);
                    Build();
                }
            })));
        Grid.SetColumn(panel, 1);
        grid.Children.Add(panel);
        return Ui.Card(grid);
    }

    private async Task AssignAsync(HubMii mii)
    {
        var targets = new List<(string Label, Func<Task<string?>> Run)>
        {
            ("Hub-Profil …", () => AssignProfileAsync(mii)),
            ("Wii (Dolphin – Mii-Datenbank)", () => Task.FromResult<string?>(App.Hub.Miis.AssignToWii(mii))),
            ("Wii U (Cemu-Konto) …", () => AssignCemuAsync(mii)),
            ("Switch (Eden – Mii-Datenbank)", () => Task.FromResult<string?>(
                App.Hub.Pipeline.IsRunning ? "Bitte zuerst das laufende Spiel beenden." : App.Hub.Miis.AddToSwitch(mii))),
            ("Switch (Eden-Benutzer benennen) …", () => AssignEdenAsync(mii)),
        };
        var idx = await Dialogs.ChooseAsync($"„{mii.Name}“ zuweisen", targets.Select(t => t.Label).ToList(),
            $"Format: {MiiCodec.PlatformName(mii.Format)}. Wii-Miis werden für Wii U (Cemu) und Switch (Eden) automatisch " +
            "umgewandelt; Wii-U-/3DS-Miis gehen nicht zurück auf die Wii.");
        if (idx < 0)
            return;
        try
        {
            var result = await targets[idx].Run();
            if (result != null)
                await Dialogs.MessageAsync("Mii zuweisen", result);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Mii-Zuweisung fehlgeschlagen", ex);
            await Dialogs.MessageAsync("Mii zuweisen", "Fehlgeschlagen: " + ex.Message);
        }
        Build(keepFocus: true);
    }

    private async Task<string?> AssignProfileAsync(HubMii mii)
    {
        var profiles = App.Hub.Profiles.All.ToList();
        var idx = await Dialogs.ChooseAsync("Profil wählen", profiles.Select(p => p.Name).ToList());
        if (idx < 0)
            return null;
        profiles[idx].MiiId = mii.Id;
        profiles[idx].Avatar.Background = mii.ColorHex;
        App.Hub.Profiles.Update(profiles[idx]);
        return $"„{mii.Name}“ ist jetzt das Mii von Profil „{profiles[idx].Name}“.";
    }

    private async Task<string?> AssignCemuAsync(HubMii mii)
    {
        var accounts = App.Hub.Miis.CemuAccountsList();
        if (accounts.Count == 0)
            return "Cemu hat noch keine Konten (Cemu einmal starten).";
        var idx = await Dialogs.ChooseAsync("Cemu-Konto wählen", accounts.Select(a => $"{a.MiiName}   ({a.PersistentId})").ToList());
        return idx < 0 ? null : App.Hub.Miis.AssignToCemu(mii, accounts[idx].PersistentId);
    }

    private async Task<string?> AssignEdenAsync(HubMii mii)
    {
        if (App.Hub.Pipeline.IsRunning)
            return "Bitte zuerst das laufende Spiel beenden.";
        var users = App.Hub.Miis.EdenUsers();
        if (users.Count == 0)
            return "Eden hat noch keine Benutzer (Eden einmal starten).";
        var idx = await Dialogs.ChooseAsync("Eden-Benutzer wählen", users.Select(u => u.Name).ToList());
        return idx < 0 ? null : App.Hub.Miis.AssignToEden(mii, users[idx].Uuid);
    }

    private async Task ImportAsync(Func<int> import, string source)
    {
        try
        {
            var n = import();
            MainWindow.Current.ShowToast(n > 0 ? $"{n} Mii(s) aus {source} übernommen ✓" : $"Keine neuen Miis in {source} gefunden");
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Mii-Import aus {source} fehlgeschlagen", ex);
            await Dialogs.MessageAsync("Mii-Import", ex.Message);
        }
        Build(keepFocus: true);
    }

    private async Task ImportFileAsync()
    {
        var file = await Dialogs.PickFileAsync(".mii", ".rcd", ".ffsd", ".cfsd", ".bin", ".3dsmii");
        if (file == null)
            return;
        try
        {
            var mii = App.Hub.Miis.ImportFile(file);
            MainWindow.Current.ShowToast($"„{mii.Name}“ importiert ✓");
        }
        catch (Exception ex)
        {
            await Dialogs.MessageAsync("Mii-Import", ex.Message);
        }
        Build(keepFocus: true);
    }

    /// <summary>Öffnet den Mii-Editor der gewählten Konsole im Emulator und übernimmt danach neue Miis.</summary>
    private async Task CreateAsync()
    {
        var dolphin = App.Hub.Adapters.Dolphin;
        var cemu = App.Hub.Adapters.Cemu;
        var eden = App.Hub.Adapters.Switch;
        var dolphinExe = dolphin.DetectInstallation().ExecutablePath;
        var cemuExe = cemu.DetectInstallation().ExecutablePath;
        var edenExe = eden.DetectInstallation().ExecutablePath;
        var miiMaker = cemuExe != null ? cemu.MiiMakerExecutable() : null;

        var options = new List<(string Label, string? Exe, string[] Args, Func<int>? Reimport, string? Missing)>
        {
            ("Wii – Mii-Kanal (Dolphin)", dolphinExe, ["-n", "0001000248414341"], App.Hub.Miis.ImportFromDolphin,
                dolphinExe == null ? "Dolphin ist nicht installiert." : !dolphin.HasMiiChannel()
                    ? "Der Mii-Kanal fehlt in Dolphin. Ihn aus deiner eigenen Wii in Dolphins NAND übernehmen (Extras → NAND-Verwaltung / Wii-Menü installieren)." : null),
            ("Wii U – Mii Maker (Cemu)", cemuExe, miiMaker != null ? ["-g", miiMaker] : [], App.Hub.Miis.ImportFromCemu,
                cemuExe == null ? "Cemu ist nicht installiert." : miiMaker == null
                    ? "Der Mii Maker fehlt in Cemu (Systemtitel aus deiner eigenen Wii U in die mlc01 übernehmen). Alternativ ein Cemu-Konto anlegen – Cemu erstellt dafür ein Standard-Mii." : null),
            ("Switch – Mii-Editor (Eden)", edenExe, [], null,
                edenExe == null ? "Eden ist nicht installiert." : null),
        };
        var idx = await Dialogs.ChooseAsync("Neues Mii erstellen", options.Select(o => o.Label + (o.Missing != null ? "   (nicht verfügbar)" : "")).ToList(),
            "Miis werden im Original-Editor der Konsole erstellt (mit deinen eigenen System-Dumps). Nach dem Schließen übernimmt der Hub neue Miis automatisch.");
        if (idx < 0)
            return;
        var o = options[idx];
        if (o.Missing != null)
        {
            await Dialogs.MessageAsync(o.Label, o.Missing);
            return;
        }
        if (idx == 2 && !await Dialogs.ConfirmAsync(o.Label, "Eden öffnet sich jetzt. Den Mii-Editor findest du dort unter Tools → Open Mii Editor " +
                                                           "(braucht deine installierte Firmware). Danach Eden schließen.", "Öffnen"))
            return;
        if (App.Hub.Pipeline.IsRunning)
        {
            await Dialogs.MessageAsync(o.Label, "Bitte zuerst das laufende Spiel beenden.");
            return;
        }

        var window = MainWindow.Current.WindowService;
        window.HideForGame();
        try
        {
            var psi = new ProcessStartInfo(o.Exe!) { WorkingDirectory = Path.GetDirectoryName(o.Exe), UseShellExecute = false };
            foreach (var a in o.Args)
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p != null)
                await p.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            HubLog.Warn("Mii-Editor konnte nicht gestartet werden", ex);
            await Dialogs.MessageAsync(o.Label, "Start fehlgeschlagen: " + ex.Message);
        }
        finally
        {
            window.RestoreAfterGame();
        }
        if (o.Reimport != null)
            await ImportAsync(o.Reimport, idx == 0 ? "Dolphin" : "Cemu");
        else
            Build();
    }

    public bool HandleNav(NavAction action) => false;
}
