using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.App.Views;

/// <summary>Einstellungen: Allgemein, Bibliothek, Emulatoren, Spielstart, Profil, Troubleshooting, Über.</summary>
public sealed partial class SettingsPage : Page, IHubPage
{
    private static readonly (string id, string label)[] Sections =
    [
        ("general", "Allgemein"), ("library", "Bibliothek"), ("emulators", "Emulatoren"), ("launch", "Spielstart"),
        ("profile", "Profil"), ("troubleshooting", "Troubleshooting"), ("about", "Über"),
    ];

    private string _section = "general";
    private string _scanText = "";

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   L/R Bereich wechseln";

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string s && Sections.Any(x => x.id == s))
            _section = s;
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Einstellungen"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 12, 0, 8) };
        foreach (var (id, label) in Sections)
        {
            var b = Ui.Action(label, null, () =>
            {
                _section = id;
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                Build(keepFocus: true);
            });
            if (id == _section)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            row.Children.Add(b);
        }
        Body.Children.Add(new ScrollViewer
        {
            Content = row,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
        });

        switch (_section)
        {
            case "general": General(); break;
            case "library": LibrarySection(); break;
            case "emulators": Emulators(); break;
            case "launch": Launch(); break;
            case "profile": Profile(); break;
            case "troubleshooting": Troubleshooting(); break;
            default: About(); break;
        }
        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private static Core.Config.HubConfig Cfg => App.Hub.Config.Current;
    private static void Save(Action<Core.Config.HubConfig> change) => App.Hub.Config.Update(change);

    private void General()
    {
        Body.Children.Add(Ui.Card(
            Ui.Toggle("Beim Start im Console Mode (randloses Vollbild)", Cfg.Ui.StartupMode == "console",
                v => Save(c => c.Ui.StartupMode = v ? "console" : "desktop")),
            Ui.Toggle("Helles Design (Basic White)", Cfg.Ui.Theme == "switch-light", v =>
            {
                Save(c => c.Ui.Theme = v ? "switch-light" : "switch");
                MainWindow.Current.ApplyTheme();
            }),
            Ui.Toggle("UI-Sounds", Cfg.Ui.Sounds, v => Save(c => c.Ui.Sounds = v)),
            Ui.Toggle("Animationen", Cfg.Ui.Animations, v => Save(c => c.Ui.Animations = v)),
            Ui.Toggle("24-Stunden-Uhr", Cfg.Ui.Clock24h, v => Save(c => c.Ui.Clock24h = v)),
            Ui.Toggle("Mario-Kart-Kacheln auch ohne Dump anzeigen", Cfg.Ui.ShowSpecialTiles, v => Save(c => c.Ui.ShowSpecialTiles = v)),
            Ui.Toggle("Mit Windows starten (direkt im Console Mode)", SystemService.Autostart, v =>
            {
                SystemService.Autostart = v;
                Save(c => c.Ui.Autostart = v);
            })));
        var volume = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (label, value) in new[] { ("Leise", 0.3), ("Mittel", 0.6), ("Laut", 0.9) })
        {
            var b = Ui.Action(label, null, () =>
            {
                Save(c => c.Ui.SoundVolume = value);
                MainWindow.Current.Sounds.Play(UiSound.Select);
                Build(keepFocus: true);
            });
            if (Math.Abs(Cfg.Ui.SoundVolume - value) < 0.05)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            volume.Children.Add(b);
        }
        Body.Children.Add(Ui.Header("Lautstärke der UI-Sounds"));
        Body.Children.Add(volume);
        Body.Children.Add(Ui.Buttons(Ui.Action("Einrichtungs-Assistent erneut starten", "",
            () => MainWindow.Current.Navigate(typeof(SetupPage)))));
    }

    private void LibrarySection()
    {
        Body.Children.Add(Ui.Subtle("Spiele müssen nicht in den Hub-Ordner kopiert werden – bestehende Speicherorte werden eingebunden. " +
                                    "Eigene Dumps werden anhand des Disc-Headers bzw. der Title-ID erkannt, Updates und DLCs automatisch zugeordnet."));
        foreach (var p in HubPlatformInfo.Emulated)
        {
            var platform = p;
            var path = Cfg.LibraryPaths.Get(platform);
            Body.Children.Add(Ui.Card(
                Ui.Text(platform.DisplayName(), 17, bold: true),
                Ui.Subtle(string.IsNullOrEmpty(path) ? "Kein Ordner gewählt" : path),
                Ui.Buttons(
                    Ui.AsyncAction("Ordner wählen", "", async () =>
                    {
                        var folder = await Dialogs.PickFolderAsync();
                        if (folder != null)
                        {
                            Save(c => c.LibraryPaths.Set(platform, folder));
                            Build(keepFocus: true);
                        }
                    }),
                    Ui.Action("Entfernen", "", () =>
                    {
                        Save(c => c.LibraryPaths.Set(platform, ""));
                        Build(keepFocus: true);
                    }))));
        }
        var extras = new List<UIElement> { Ui.Text("Weitere Ordner (Plattform automatisch)", 17, bold: true) };
        foreach (var f in Cfg.ExtraLibraryFolders.ToList())
        {
            var folder = f;
            extras.Add(Ui.Buttons(Ui.Text(folder), Ui.Action("Entfernen", "", () =>
            {
                Save(c => c.ExtraLibraryFolders.Remove(folder));
                Build(keepFocus: true);
            })));
        }
        extras.Add(Ui.Buttons(Ui.AsyncAction("Ordner hinzufügen", "", async () =>
        {
            var folder = await Dialogs.PickFolderAsync();
            if (folder != null && !Cfg.ExtraLibraryFolders.Contains(folder))
            {
                Save(c => c.ExtraLibraryFolders.Add(folder));
                Build(keepFocus: true);
            }
        })));
        Body.Children.Add(Ui.Card([.. extras]));
        Body.Children.Add(Ui.Buttons(Ui.AsyncAction("Bibliothek jetzt scannen", "", async () =>
        {
            _scanText = "Scanne …";
            Build(keepFocus: true);
            var result = await App.Hub.RefreshLibraryAsync();
            _scanText = $"{App.Hub.Library.Games.Count(g => !g.IsPlaceholder)} Spiele · {result.FilesChecked} Dateien geprüft · " +
                        $"{result.SwitchAddOns.Values.Sum(a => a.Dlcs.Count)} DLC(s), {result.SwitchAddOns.Values.Sum(a => a.Updates.Count)} Update(s) erkannt";
            Build(keepFocus: true);
        })));
        if (!string.IsNullOrEmpty(_scanText))
            Body.Children.Add(Ui.Subtle(_scanText));
    }

    private void Emulators()
    {
        foreach (var adapter in App.Hub.Adapters.All)
        {
            var a = adapter;
            var inst = a.DetectInstallation();
            var items = new List<UIElement>
            {
                Ui.Text(a.DisplayName, 17, bold: true),
                Ui.Status(inst.IsInstalled ? $"{inst.ExecutablePath}  ({inst.Version})" : "Nicht gefunden", inst.IsInstalled),
            };
            if (inst.UserDataDir != null)
                items.Add(Ui.Subtle("Daten: " + inst.UserDataDir));
            foreach (var p in inst.Problems)
                items.Add(Ui.Status(p, false));
            items.Add(Ui.Buttons(
                Ui.AsyncAction("EXE manuell wählen", "", async () =>
                {
                    var file = await Dialogs.PickFileAsync(".exe");
                    if (file != null)
                    {
                        Save(c => c.Emulators.Set(a.Id, file));
                        Build(keepFocus: true);
                    }
                }),
                Ui.Action("Automatisch erkennen", "", () =>
                {
                    Save(c => c.Emulators.Set(a.Id, ""));
                    Build(keepFocus: true);
                }),
                Ui.Action("Ordner öffnen", "", () => SystemService.OpenFolder(inst.UserDataDir ?? inst.ExecutablePath))));
            Body.Children.Add(Ui.Card([.. items]));
        }
        Body.Children.Add(Ui.Card(
            Ui.Text("Switch-Emulator", 17, bold: true),
            Ui.Subtle("Der Switch-Emulator ist als Adapter austauschbar. Registrierte Adapter: " +
                      string.Join(", ", App.Hub.Adapters.SwitchAdapterIds) + ". Aktiv: " + Cfg.Emulators.SwitchAdapter)));
        var ww = App.Hub.Adapters.WheelWizard;
        Body.Children.Add(Ui.Card(
            Ui.Text("Wheel Wizard", 17, bold: true),
            Ui.Status(ww.FindExecutable() ?? "Nicht installiert", ww.FindExecutable() != null),
            Ui.Subtle("Konfiguration: " + ww.ConfigFile),
            Ui.Buttons(
                Ui.Action("Mit Hub-Dolphin verbinden", "", () =>
                {
                    App.Hub.ConfigureWheelWizard();
                    MainWindow.Current.ShowToast("Wheel Wizard nutzt jetzt Dolphin und Spiel des Hubs ✓");
                }),
                Ui.Action("Wheel Wizard öffnen", "", () => ww.Open()))));
    }

    private void Launch()
    {
        Body.Children.Add(Ui.Card(
            Ui.Toggle("Hub während des Spiels ausblenden (sonst minimieren)", Cfg.Launch.HideHubWhilePlaying, v => Save(c => c.Launch.HideHubWhilePlaying = v)),
            Ui.Toggle("Spiele im Vollbild starten", Cfg.Launch.EmulatorFullscreen, v => Save(c => c.Launch.EmulatorFullscreen = v)),
            Ui.Toggle("Home + Minus (1,5 s halten) beendet das laufende Spiel", Cfg.Launch.HomeComboStopsGame, v => Save(c => c.Launch.HomeComboStopsGame = v)),
            Ui.Toggle("Vor dem Start Konfiguration sichern", Cfg.Launch.BackupBeforeLaunch, v => Save(c => c.Launch.BackupBeforeLaunch = v))));
        Body.Children.Add(Ui.Subtle("Ablauf: Preset laden → Dateien prüfen → Controllerprofil → Mods aktivieren → Emulator starten → Hub ausblenden → " +
                                    "Spiel läuft → Spielzeit speichern → Hub wieder anzeigen."));
    }

    private void Profile()
    {
        var profile = App.Hub.Profiles.Active;
        var avatar = new AvatarView { Width = 150, Height = 150, Spec = profile.Avatar, HorizontalAlignment = HorizontalAlignment.Left };
        var name = new TextBox { Header = "Name", Text = profile.Name, MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
        name.LostFocus += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(name.Text) && name.Text != profile.Name)
            {
                profile.Name = name.Text.Trim();
                App.Hub.Profiles.Update(profile);
            }
        };
        var s = profile.Avatar;
        void Changed()
        {
            App.Hub.Profiles.Update(profile);
            avatar.Rebuild();
            MainWindow.Current.Sounds.Play(UiSound.Toggle);
        }
        static string Next(IReadOnlyList<string> list, string current) => list[(list.ToList().IndexOf(current) + 1 + list.Count) % list.Count];

        Body.Children.Add(Ui.Card(
            avatar,
            name,
            Ui.Buttons(
                Ui.Action("Hintergrund", "", () => { s.Background = Next(AvatarSpec.Backgrounds, s.Background); Changed(); }),
                Ui.Action("Haut", "", () => { s.Skin = Next(AvatarSpec.Skins, s.Skin); Changed(); }),
                Ui.Action("Haarfarbe", "", () => { s.Hair = Next(AvatarSpec.Hairs, s.Hair); Changed(); }),
                Ui.Action("Frisur", "", () => { s.HairStyle = (s.HairStyle + 1) % AvatarSpec.HairStyleCount; Changed(); }),
                Ui.Action("Augen", "", () => { s.Eyes = (s.Eyes + 1) % AvatarSpec.EyesCount; Changed(); }),
                Ui.Action("Mund", "", () => { s.Mouth = (s.Mouth + 1) % AvatarSpec.MouthCount; Changed(); }),
                Ui.Action("Shirt", "", () => { s.Shirt = Next(AvatarSpec.Backgrounds, s.Shirt); Changed(); }))));

        Body.Children.Add(Ui.Header("Profile"));
        var list = new List<UIElement>();
        foreach (var p in App.Hub.Profiles.All)
        {
            var prof = p;
            var b = Ui.Action(prof.Name + (prof.Id == profile.Id ? "  ✓" : ""), null, () =>
            {
                App.Hub.Profiles.SetActive(prof);
                Build(keepFocus: true);
            });
            list.Add(b);
        }
        list.Add(Ui.AsyncAction("Profil hinzufügen", "", async () =>
        {
            var box = new TextBox { Header = "Name" };
            if (await Dialogs.ShowAsync("Neues Profil", box, "Anlegen", "Abbrechen") == ContentDialogResult.Primary &&
                !string.IsNullOrWhiteSpace(box.Text))
            {
                App.Hub.Profiles.SetActive(App.Hub.Profiles.Add(box.Text.Trim()));
                Build();
            }
        }));
        Body.Children.Add(Ui.Buttons([.. list]));
    }

    private void Troubleshooting()
    {
        Body.Children.Add(Ui.Card(
            Ui.Text("Logs", 17, bold: true),
            Ui.Subtle("Pro Spielstart werden Spiel, Preset, Emulator, Argumente, Fehler, Exit-Code und Laufzeit protokolliert."),
            Ui.Buttons(
                Ui.Action("View Logs", "", () => MainWindow.Current.Navigate(typeof(LogsPage))),
                Ui.Action("Log-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Paths.Logs)))));
        Body.Children.Add(Ui.Card(
            Ui.Text("Backups", 17, bold: true),
            Ui.Buttons(
                Ui.Action("Restore Backup", "", () => MainWindow.Current.Navigate(typeof(ModsPage))),
                Ui.Action("Backup-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Backups.Root)))));
        Body.Children.Add(Ui.Card(
            Ui.Text("Ordner", 17, bold: true),
            Ui.Buttons(
                Ui.Action("Hub-Daten (data)", "", () => SystemService.OpenFolder(App.Hub.Paths.Data)),
                Ui.Action("Komponenten (integrations)", "", () => SystemService.OpenFolder(App.Hub.Paths.Integrations)),
                Ui.Action("Artwork", "", () => SystemService.OpenFolder(App.Hub.Paths.Artwork)))));
    }

    private void About()
    {
        Body.Children.Add(Ui.Card(
            Ui.Text($"Emulator PC Hub {EmulatorPCHub.Core.HubInfo.DisplayVersion}", 20, bold: true),
            Ui.Subtle("Eigene Oberfläche für GameCube, Wii, Wii U und Switch auf dem PC – mit Dolphin, Cemu, Eden, WiiCompiled, " +
                      "Wheel Wizard, Retro Rewind und CTGP Deluxe."),
            Ui.Subtle("Stammordner: " + App.Hub.Paths.Root),
            Ui.Subtle("Dies ist kein offizielles Nintendo-Produkt. Es werden keine Spiele, Firmware, Keys oder Nintendo-Assets " +
                      "mitgeliefert oder heruntergeladen – nur deine eigenen Dumps werden verwendet.")));
    }

    public bool HandleNav(NavAction action)
    {
        if (action is not (NavAction.L or NavAction.R))
            return false;
        var idx = Array.FindIndex(Sections, s => s.id == _section);
        idx = (idx + (action == NavAction.R ? 1 : -1) + Sections.Length) % Sections.Length;
        _section = Sections[idx].id;
        MainWindow.Current.Sounds.Play(UiSound.Toggle);
        Build();
        return true;
    }
}
