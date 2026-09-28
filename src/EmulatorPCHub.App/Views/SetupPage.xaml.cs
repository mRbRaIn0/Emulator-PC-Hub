using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Installations-Assistent beim ersten Start (Plan Abschnitt 14):
/// Spieleordner → Emulatoren erkennen → fehlende Komponenten → Controller → Pfade prüfen →
/// Spiele importieren → Cover → Startmenü.
/// </summary>
public sealed partial class SetupPage : Page, IHubPage
{
    private static readonly string[] Steps =
    [
        "Spieleordner auswählen", "Emulatoren erkennen", "Fehlende Komponenten", "Controller erkennen",
        "Pfade prüfen", "Spiele importieren", "Cover", "Startmenü",
    ];

    private int _step;
    private string _result = "";

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   R Weiter   L Zurück";

    public SetupPage()
    {
        InitializeComponent();
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title(_step == 0 ? "Welcome to Emulator PC Hub" : Steps[_step]));
        Body.Children.Add(Ui.Subtle($"Schritt {_step + 1} von {Steps.Length}: {Steps[_step]}"));
        var progress = new ProgressBar { Maximum = Steps.Length, Value = _step + 1, Margin = new Thickness(0, 10, 0, 16) };
        Body.Children.Add(progress);

        switch (_step)
        {
            case 0: StepFolders(); break;
            case 1: StepEmulators(); break;
            case 2: StepComponents(); break;
            case 3: StepControllers(); break;
            case 4: StepPaths(); break;
            case 5: StepImport(); break;
            case 6: StepCovers(); break;
            default: StepStartMenu(); break;
        }

        var nav = new List<UIElement>();
        if (_step > 0)
            nav.Add(Ui.Action("Zurück", "", () => Go(-1)));
        nav.Add(_step < Steps.Length - 1
            ? Ui.Action("Weiter", "", () => Go(1), primary: true)
            : Ui.Action("Fertig – zum Hub", "", Finish, primary: true));
        nav.Add(Ui.Action("Überspringen", null, Finish));
        Body.Children.Add(new Border { Height = 16 });
        Body.Children.Add(Ui.Buttons([.. nav]));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private void Go(int delta)
    {
        _step = Math.Clamp(_step + delta, 0, Steps.Length - 1);
        _result = "";
        MainWindow.Current.Sounds.Play(delta > 0 ? UiSound.Select : UiSound.Back);
        Build();
        if (_step == 5)
            _ = ImportAsync();
    }

    private void Finish()
    {
        App.Hub.Config.Update(c => c.SetupCompleted = true);
        MainWindow.Current.Sounds.Play(UiSound.Launch);
        MainWindow.Current.GoHome();
    }

    private void StepFolders()
    {
        Body.Children.Add(Ui.Text("Wähle die Ordner mit deinen eigenen Spiele-Dumps. Spiele müssen nicht verschoben werden."));
        foreach (var p in HubPlatformInfo.Emulated)
        {
            var platform = p;
            var path = App.Hub.Config.Current.LibraryPaths.Get(platform);
            Body.Children.Add(Ui.Card(
                Ui.Text(platform.DisplayName(), 17, bold: true),
                Ui.Subtle(string.IsNullOrEmpty(path) ? "Noch kein Ordner" : path),
                Ui.Buttons(Ui.AsyncAction("Ordner wählen", "", async () =>
                {
                    var folder = await Dialogs.PickFolderAsync();
                    if (folder != null)
                    {
                        App.Hub.Config.Update(c => c.LibraryPaths.Set(platform, folder));
                        Build(keepFocus: true);
                    }
                }))));
        }
        var suggestions = SuggestFolders();
        if (suggestions.Count > 0)
        {
            Body.Children.Add(Ui.Header("Gefundene Ordner"));
            foreach (var (folder, platform) in suggestions)
            {
                var f = folder;
                var pl = platform;
                Body.Children.Add(Ui.Buttons(Ui.Action($"{f} als {pl.ShortName()} übernehmen", "", () =>
                {
                    App.Hub.Config.Update(c => c.LibraryPaths.Set(pl, f));
                    Build(keepFocus: true);
                })));
            }
        }
    }

    /// <summary>Schlägt typische Spieleordner auf den Laufwerken vor (nur oberste Ebenen, nur lesend).</summary>
    private static List<(string folder, HubPlatform platform)> SuggestFolders()
    {
        var result = new List<(string, HubPlatform)>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                foreach (var dir in SafeDirs(drive.RootDirectory.FullName).Concat(SafeDirs(Path.Combine(drive.RootDirectory.FullName, "Games"))))
                {
                    var name = Path.GetFileName(dir).ToLowerInvariant().Replace(" ", "");
                    HubPlatform? p = name switch
                    {
                        "wiiu" or "wii-u" or "wiiugames" => HubPlatform.WiiU,
                        "wii" or "wiigames" => HubPlatform.Wii,
                        "gamecube" or "gc" or "ngc" => HubPlatform.GameCube,
                        "switch" or "nsp" or "switchgames" => HubPlatform.Switch,
                        "ds" or "nds" or "nintendods" or "dsgames" => HubPlatform.DS,
                        "3ds" or "n3ds" or "nintendo3ds" or "3dsgames" => HubPlatform.ThreeDS,
                        _ => null,
                    };
                    if (p != null && string.IsNullOrEmpty(App.Hub.Config.Current.LibraryPaths.Get(p.Value)))
                        result.Add((dir, p.Value));
                }
            }
        }
        catch (Exception)
        {
        }
        return result.Take(8).ToList();
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetDirectories(path) : []; }
        catch (Exception) { return []; }
    }

    private void StepEmulators()
    {
        foreach (var adapter in App.Hub.Adapters.All)
        {
            var inst = adapter.DetectInstallation();
            Body.Children.Add(Ui.Status(inst.IsInstalled ? $"{adapter.DisplayName} {inst.Version} gefunden" : $"{adapter.DisplayName} nicht gefunden", inst.IsInstalled));
        }
        var ww = App.Hub.Adapters.WheelWizard.FindExecutable();
        Body.Children.Add(Ui.Status(ww != null ? "Wheel Wizard gefunden" : "Wheel Wizard nicht gefunden", ww != null));
    }

    private void StepComponents()
    {
        var missing = App.Hub.ComponentStatus().Where(c => c.Status is ComponentStatus.NotInstalled or ComponentStatus.NeedsAttention).ToList();
        if (missing.Count == 0)
            Body.Children.Add(Ui.Status("Alle Komponenten sind installiert.", true));
        foreach (var c in missing)
        {
            Body.Children.Add(Ui.Status($"{c.Name}: {c.StatusText}", false));
            foreach (var p in c.Problems)
                Body.Children.Add(Ui.Subtle("   " + p));
        }
        Body.Children.Add(Ui.Buttons(Ui.Action("Zu Komponenten", "", () => MainWindow.Current.Navigate(typeof(DownloadsPage)))));
    }

    private void StepControllers()
    {
        var list = MainWindow.Current.Controllers.Devices;
        if (list.Count == 0)
            Body.Children.Add(Ui.Status("Kein Controller gefunden – Tastatur funktioniert immer (Pfeile, Enter, Esc).", false));
        foreach (var c in list)
            Body.Children.Add(Ui.Status($"{(c.Player > 0 ? $"Spieler {c.Player}" : "Nicht zugewiesen")}: {c.Name} ({c.Kind.DisplayName()}, Akku {c.BatteryText})", true));
        Body.Children.Add(Ui.Buttons(Ui.Action("Controller verwalten", "", () => MainWindow.Current.Navigate(typeof(ControllersPage)))));
        Body.Children.Add(Ui.Buttons(Ui.Action("Erneut suchen", "", () => Build(keepFocus: true))));
    }

    private void StepPaths()
    {
        var dolphin = App.Hub.Adapters.Dolphin.DetectInstallation();
        Body.Children.Add(Ui.Status("Hub-Stammordner: " + App.Hub.Paths.Root, true));
        Body.Children.Add(Ui.Status("Dolphin-Userordner: " + App.Hub.Adapters.Dolphin.UserDirectory(), dolphin.IsInstalled));
        var rr = App.Hub.RetroRewind.LocalStatus();
        Body.Children.Add(Ui.Status(rr.Installed ? $"Retro Rewind {rr.Version}: {rr.Folder}" : "Retro Rewind nicht installiert", rr.Installed));
        var ww = App.Hub.Adapters.WheelWizard.ReadConfig();
        Body.Children.Add(Ui.Status("Wheel Wizard → Dolphin: " + (ww.DolphinLocation ?? "nicht verbunden"), ww.DolphinLocation != null));
        Body.Children.Add(Ui.Buttons(Ui.Action("Wheel Wizard mit Hub verbinden", "", () =>
        {
            App.Hub.ConfigureWheelWizard();
            Build(keepFocus: true);
        })));
    }

    private void StepImport()
    {
        Body.Children.Add(Ui.Text(string.IsNullOrEmpty(_result) ? "Spiele werden gesucht …" : _result));
        Body.Children.Add(Ui.Buttons(Ui.AsyncAction("Erneut scannen", "", ImportAsync)));
    }

    private async Task ImportAsync()
    {
        var scan = await App.Hub.RefreshLibraryAsync();
        var games = App.Hub.Library.Games.Where(g => !g.IsPlaceholder).ToList();
        _result = $"{games.Count} Spiele importiert ({scan.FilesChecked} Dateien geprüft).\n" +
                  string.Join("\n", games.GroupBy(g => g.Platform).Select(gr => $"{gr.Key.DisplayName()}: {gr.Count()}")) +
                  (App.Hub.Library.MarioKartWiiDumps.Count > 0 ? "\nMario Kart Wii erkannt ✓" : "") +
                  (App.Hub.Library.MarioKart8DeluxeDumps.Count > 0 ? "\nMario Kart 8 Deluxe erkannt ✓" : "");
        if (_step == 5)
            Build(keepFocus: true);
    }

    private void StepCovers()
    {
        Body.Children.Add(Ui.Text("Cover und Hintergründe werden lokal verwaltet – der Hub liefert keine fremden Bilder mit."));
        Body.Children.Add(Ui.Subtle("Möglichkeiten:\n• Auf der Spielseite „Cover wählen“\n• Bild mit gleichem Namen neben die Spieldatei legen (z. B. Spiel.png)\n" +
                                    "• data/artwork/<Spiel-ID>/cover.png und background.png\nOhne Cover erzeugt der Hub eigene Kacheln."));
        Body.Children.Add(Ui.Buttons(Ui.Action("Artwork-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Paths.Artwork))));
    }

    private void StepStartMenu()
    {
        Body.Children.Add(Ui.Text("Fast fertig! Soll der Hub im Startmenü erscheinen oder direkt mit Windows starten?"));
        Body.Children.Add(Ui.Buttons(Ui.Action("Startmenü-Verknüpfung erstellen", "", () =>
        {
            var link = SystemService.CreateStartMenuShortcut();
            MainWindow.Current.ShowToast(link != null ? "Verknüpfung erstellt ✓" : "Verknüpfung konnte nicht erstellt werden");
        })));
        Body.Children.Add(Ui.Toggle("Mit Windows starten (Console Mode)", SystemService.Autostart, v => SystemService.Autostart = v));
    }

    public bool HandleNav(NavAction action)
    {
        switch (action)
        {
            case NavAction.R:
                if (_step < Steps.Length - 1) Go(1); else Finish();
                return true;
            case NavAction.L:
                Go(-1);
                return true;
            default:
                return false;
        }
    }
}
