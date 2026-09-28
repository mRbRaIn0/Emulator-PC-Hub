using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Zentrale Mod-Seite (Plan Abschnitt 13): Mario Kart Wii (Retro Rewind), Mario Kart 8 Deluxe (CTGP Deluxe
/// und eigene Mods), Cemu Graphic Packs, Backups mit Wiederherstellen.
/// </summary>
public sealed partial class ModsPage : Page, IHubPage
{
    private bool _busy;
    private string _busyText = "";

    public string Hints => "Ⓐ Umschalten   Ⓑ Zurück";

    public ModsPage()
    {
        InitializeComponent();
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Mods"));
        if (_busy)
        {
            Body.Children.Add(new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0, 8, 0, 4) });
            Body.Children.Add(Ui.Subtle(_busyText));
        }

        // ---- Mario Kart Wii ----
        Body.Children.Add(Ui.Header("Mario Kart Wii"));
        var rr = App.Hub.RetroRewind.LocalStatus();
        var mkwii = new List<UIElement>
        {
            Ui.Status(rr.Installed ? $"Retro Rewind {rr.Version}   Enabled (als Edition wählbar)" : "Retro Rewind nicht installiert", rr.Installed),
            Ui.Toggle("My-Stuff-Mods (eigene Mods in Retro Rewind)", App.Hub.Config.Current.MarioKartWii.RetroRewindMyStuff,
                v => App.Hub.Config.Update(c => c.MarioKartWii.RetroRewindMyStuff = v)),
        };
        var wwMods = Path.Combine(App.Hub.Adapters.WheelWizard.DataFolder(), "Mods");
        if (Directory.Exists(wwMods))
        {
            foreach (var dir in Directory.GetDirectories(wwMods))
                mkwii.Add(Ui.Subtle("• " + Path.GetFileName(dir) + " (Wheel Wizard)"));
        }
        mkwii.Add(Ui.Buttons(
            Ui.Action("Mario-Kart-Wii-Seite", "", () => MainWindow.Current.Navigate(typeof(MarioKartWiiPage))),
            Ui.Action("Mods in Wheel Wizard verwalten", "", () =>
            {
                if (!App.Hub.Adapters.WheelWizard.Open())
                    MainWindow.Current.ShowToast("Wheel Wizard ist nicht installiert (Komponenten).");
            })));
        Body.Children.Add(Ui.Card([.. mkwii]));

        // ---- Mario Kart 8 Deluxe ----
        Body.Children.Add(Ui.Header("Mario Kart 8 Deluxe"));
        var mk8 = new List<UIElement>();
        var mods = App.Hub.SwitchMods.List(KnownGames.MarioKart8DeluxeTitleId, "Mario Kart 8 Deluxe");
        if (mods.Count == 0)
            mk8.Add(Ui.Subtle("Keine Mods vorhanden."));
        foreach (var mod in mods)
        {
            var m = mod;
            mk8.Add(Ui.Toggle($"{m.Name}{(m.Version != null ? "  v" + m.Version : "")}   {(m.Enabled ? "Enabled" : "Disabled")}", m.Enabled,
                v => _ = ToggleSwitchModAsync(m, v)));
        }
        mk8.Add(Ui.Subtle("Beim Start setzt das gewählte Preset die Mods automatisch: Vanilla = keine Mods, CTGP Deluxe = nur CTGP, Custom = deine Auswahl."));
        mk8.Add(Ui.Buttons(
            Ui.AsyncAction("Mod importieren (ZIP)", "", () => ImportSwitchModAsync(folder: false)),
            Ui.AsyncAction("Mod importieren (Ordner)", "", () => ImportSwitchModAsync(folder: true)),
            Ui.Action("Mod-Speicher öffnen", "", () =>
            {
                var dir = App.Hub.SwitchMods.StoreDirectory(KnownGames.MarioKart8DeluxeTitleId);
                Directory.CreateDirectory(dir);
                SystemService.OpenFolder(dir);
            })));
        Body.Children.Add(Ui.Card([.. mk8]));

        // ---- Cemu Graphic Packs ----
        Body.Children.Add(Ui.Header("Cemu – Graphic Packs"));
        var packs = new List<UIElement>();
        if (!App.Hub.Adapters.Cemu.DetectInstallation().IsInstalled)
        {
            packs.Add(Ui.Subtle("Cemu ist nicht installiert."));
        }
        else
        {
            var list = App.Hub.GraphicPacks.List();
            if (list.Count == 0)
                packs.Add(Ui.Subtle("Noch keine Graphic Packs. In Cemu: Optionen → Graphic Packs → „Download latest community graphic packs“."));
            foreach (var pack in list.Take(60))
            {
                var p = pack;
                packs.Add(Ui.Toggle(p.Name, p.Enabled, v =>
                {
                    App.Hub.GraphicPacks.SetEnabled(p, v);
                    MainWindow.Current.Sounds.Play(UiSound.Toggle);
                }));
            }
        }
        Body.Children.Add(Ui.Card([.. packs]));

        // ---- Backups ----
        Body.Children.Add(Ui.Header("Backups (Restore Backup)"));
        var backups = App.Hub.Backups.List().Take(12).ToList();
        var backupItems = new List<UIElement>();
        if (backups.Count == 0)
            backupItems.Add(Ui.Subtle("Noch keine Backups. Vor Änderungen an Mods, Configs und Presets wird automatisch gesichert."));
        foreach (var b in backups)
        {
            var entry = b;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(Ui.Text($"{entry.Category} · {entry.Label} · {entry.CreatedAt.LocalDateTime:g} · {Format.Size(entry.SizeBytes)}"));
            if (!string.IsNullOrEmpty(entry.OriginalPath))
            {
                var restore = Ui.AsyncAction("Wiederherstellen", "", () => RestoreAsync(entry));
                Grid.SetColumn(restore, 1);
                row.Children.Add(restore);
            }
            backupItems.Add(row);
        }
        backupItems.Add(Ui.Buttons(Ui.Action("Backup-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Backups.Root))));
        Body.Children.Add(Ui.Card([.. backupItems]));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private async Task ToggleSwitchModAsync(ModInfo mod, bool enabled)
    {
        _busy = true;
        _busyText = $"{mod.Name} wird {(enabled ? "aktiviert" : "deaktiviert")} …";
        Build(keepFocus: true);
        try
        {
            await App.Hub.SwitchMods.SetEnabledAsync(mod, enabled, new Progress<string>(s => _busyText = s));
            MainWindow.Current.Sounds.Play(UiSound.Toggle);
        }
        catch (Exception ex)
        {
            await Dialogs.MessageAsync("Mod", ex.Message);
        }
        finally
        {
            _busy = false;
            Build(keepFocus: true);
        }
    }

    private async Task ImportSwitchModAsync(bool folder)
    {
        var source = folder ? await Dialogs.PickFolderAsync() : await Dialogs.PickFileAsync(".zip");
        if (source == null)
            return;
        _busy = true;
        _busyText = "Mod wird importiert …";
        Build(keepFocus: true);
        try
        {
            var name = Path.GetFileNameWithoutExtension(source);
            if (name.Contains("CTGP", StringComparison.OrdinalIgnoreCase))
                name = Mods.MarioKart8DeluxeService.CtgpFolderName;
            await App.Hub.SwitchMods.InstallAsync(source, KnownGames.MarioKart8DeluxeTitleId, name, new Progress<string>(s => _busyText = s));
            MainWindow.Current.ShowToast($"Mod „{name}“ importiert ✓");
        }
        catch (Exception ex)
        {
            await Dialogs.MessageAsync("Mod-Import", ex.Message);
        }
        finally
        {
            _busy = false;
            Build(keepFocus: true);
        }
    }

    private async Task RestoreAsync(BackupEntry entry)
    {
        if (!await Dialogs.ConfirmAsync("Backup wiederherstellen",
                $"„{entry.Label}“ vom {entry.CreatedAt.LocalDateTime:g} wiederherstellen?\n\nZiel: {entry.OriginalPath}\n\nDer aktuelle Stand wird vorher gesichert.",
                "Wiederherstellen"))
            return;
        var ok = App.Hub.Backups.Restore(entry);
        MainWindow.Current.ShowToast(ok ? "Backup wiederhergestellt ✓" : "Wiederherstellung fehlgeschlagen (siehe Logs)");
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action) => false;
}
