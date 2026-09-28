using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Mario-Kart-Wii-Seite (Plan 6.1): Spielen, Edition (Vanilla / Retro Rewind / eigenes Preset),
/// Engine (WiiCompiled / Dolphin), Controller, Mods, Strecken, Einstellungen.
/// Wheel Wizard muss im Normalbetrieb nicht geöffnet werden.
/// </summary>
public sealed partial class MarioKartWiiPage : Page, IHubPage
{
    private readonly MarioKartWiiViewModel _vm = new(App.Hub);
    private Button? _playButton;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Retro Rewind prüfen   Ⓨ Spielen";

    public MarioKartWiiPage()
    {
        InitializeComponent();
    }

    public void OnShown()
    {
        _vm.Reload();
        var setup = App.Hub.Input.Resolve(_vm.Game, App.Hub.Input.BackendFor(_vm.Game));
        _vm.ControllerText = setup.Players.Count > 0
            ? string.Join("   ", setup.Players.Select(p => $"P{p.Player}: {p.Device.Name}{(p.Mode == Core.Input.PlayerMode.Compatibility ? " (PadForge)" : "")}"))
            : "Kein Controller – Tastatur";
        Build();
        MainWindow.Current.SetBackdrop("#FF1E88E5", "#FF0D3C7A", _vm.Game.BackgroundPath ?? _vm.Game.CoverPath);
        _ = CheckUpdatesQuietlyAsync();
    }

    private async Task CheckUpdatesQuietlyAsync()
    {
        await _vm.CheckRetroRewindUpdateAsync();
        if (ReferenceEquals(MainWindow.Current.CurrentPage, this))
            Build(keepFocus: true);
    }

    private void Build(bool keepFocus = false)
    {
        var focusIndex = keepFocus ? FocusKeeper.Capture(this) : -1;
        Left.Children.Clear();
        Right.Children.Clear();

        // ---- Links: Titel, Spielen, Edition, Engine ----
        Left.Children.Add(Ui.Title("Mario Kart Wii"));
        Left.Children.Add(Ui.Subtle($"Spielzeit: {_vm.PlayTimeText}   ·   {_vm.ReadyText}"));

        _playButton = Ui.Action("Spielen", "", () => _ = PlayAsync(), primary: true);
        _playButton.Margin = new Thickness(0, 16, 0, 6);
        Left.Children.Add(_playButton);

        Left.Children.Add(Ui.Header("Edition"));
        foreach (var e in _vm.Editions)
            Left.Children.Add(Ui.Option(e, item =>
            {
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                _vm.SelectEdition(item);
                Build(keepFocus: true);
            }));

        Left.Children.Add(Ui.Header("Engine"));
        foreach (var e in _vm.Engines)
            Left.Children.Add(Ui.Option(e, item =>
            {
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                _vm.SelectEngine(item);
                Build(keepFocus: true);
            }));

        Left.Children.Add(Ui.Header("Controller"));
        if (ControllerAdviceView.For(_vm.Game) is { } advice)
            Left.Children.Add(ControllerAdviceView.Details(advice));
        Left.Children.Add(Ui.Text(_vm.ControllerText, 17, bold: true));
        Left.Children.Add(Ui.Subtle("Mit Dolphin schreibt der Hub die Belegung automatisch (Profil pro Spiel). WiiCompiled: Feinbelegung im Spiel mit F10."));
        Left.Children.Add(Ui.Buttons(Ui.Action("Controller-Profil für Mario Kart Wii", "",
            () => MainWindow.Current.Navigate(typeof(ControllerProfilePage), _vm.Game.Id))));

        Left.Children.Add(Ui.Buttons(
            Ui.Action("Mods", "", () => MainWindow.Current.Navigate(typeof(ModsPage))),
            Ui.Action("Strecken", "", () => _ = ShowTracksAsync()),
            Ui.Action("Einstellungen", "", () => _ = ShowSettingsAsync())));

        // ---- Rechts: Status & Einrichtung ----
        Right.Children.Add(Ui.Header("Status"));
        Right.Children.Add(Ui.Card(
            Ui.Status(_vm.DumpText, _vm.HasDump),
            Ui.Status(_vm.RetroRewindText, _vm.RetroRewindInstalled && !_vm.RetroRewindUpdate),
            Ui.Status(_vm.WiiCompiledText, _vm.WiiCompiledInstalled),
            Ui.Status(_vm.DolphinText, _vm.DolphinInstalled),
            Ui.Status(_vm.WheelWizardText, _vm.WheelWizardInstalled)));

        var setup = new List<UIElement>
        {
            Ui.AsyncAction(_vm.HasDump ? "Anderen Dump wählen" : "Eigenen Dump wählen", "", ChooseDumpAsync),
        };
        if (_vm.CanInstallWiiCompiled)
            setup.Add(Ui.AsyncAction("WiiCompiled installieren", "", InstallWiiCompiledAsync));
        if (!_vm.RetroRewindInstalled)
            setup.Add(Ui.AsyncAction("Retro Rewind installieren", "", InstallRetroRewindAsync));
        else if (_vm.RetroRewindUpdate)
            setup.Add(Ui.AsyncAction("Retro Rewind aktualisieren", "", InstallRetroRewindAsync));
        if (!_vm.RetroRewindInstalled || _vm.RetroRewindUpdate)
            setup.Add(Ui.Action("Retro-Rewind-Webseite", "", () => SystemService.OpenUrl("https://rwfc.net")));
        Right.Children.Add(Ui.Header("Einrichtung"));
        Right.Children.Add(Ui.Buttons([.. setup]));
        if (_vm.IsBusy)
        {
            Right.Children.Add(new ProgressBar { IsIndeterminate = _vm.BusyPercent <= 0, Value = _vm.BusyPercent, Margin = new Thickness(0, 6, 0, 6) });
            Right.Children.Add(Ui.Subtle(_vm.BusyText));
        }

        Right.Children.Add(Ui.Header("Advanced Tools"));
        Right.Children.Add(Ui.Buttons(
            Ui.Action("Wheel Wizard öffnen", "", () =>
            {
                if (!App.Hub.Adapters.WheelWizard.Open())
                    MainWindow.Current.ShowToast("Wheel Wizard ist nicht installiert (Komponenten).");
            }),
            Ui.Action("Dolphin-Ordner", "", () => SystemService.OpenFolder(App.Hub.Adapters.Dolphin.UserDirectory()))));
        Right.Children.Add(Ui.Subtle(
            "Der normale Weg ist: Hub → Mario Kart Wii → Spielen. Wheel Wizard wird nur für Sonderfälle gebraucht."));

        Right.Children.Add(Ui.Card(
            Ui.Text("Eigene Spielkopie", 15, bold: true),
            Ui.Subtle("Der Hub verteilt keine Spiele. Lege deinen eigenen Mario-Kart-Wii-Dump (ISO/WBFS/RVZ) in einen " +
                      "Bibliotheksordner oder wähle ihn oben aus. WiiCompiled braucht die PAL-Version (RMCP01), " +
                      "Dolphin funktioniert mit allen Regionen.")));

        if (!keepFocus)
            DispatcherQueue.TryEnqueue(() => _playButton?.Focus(FocusState.Keyboard));
        else
            FocusKeeper.Restore(this, focusIndex);
    }

    private async Task PlayAsync()
    {
        if (!_vm.CanPlay)
        {
            MainWindow.Current.Sounds.Play(UiSound.Error);
            MainWindow.Current.ShowToast(_vm.ReadyText);
            return;
        }
        await MainWindow.Current.LaunchAsync(_vm.Game, _vm.SelectedPreset);
    }

    private async Task ChooseDumpAsync()
    {
        var file = await Dialogs.PickFileAsync(".iso", ".wbfs", ".rvz", ".gcz", ".ciso", ".wia");
        if (file == null)
            return;
        var error = _vm.SetDump(file);
        if (error != null)
            await Dialogs.MessageAsync("Dump nicht erkannt", error);
        else
            MainWindow.Current.ShowToast("Mario-Kart-Wii-Dump übernommen ✓");
        Build();
    }

    private async Task InstallWiiCompiledAsync()
    {
        if (!await Dialogs.ConfirmAsync("WiiCompiled installieren",
                "WiiCompiled übersetzt deinen eigenen Mario-Kart-Wii-Dump in ein natives PC-Programm (inkl. Retro Rewind, falls installiert).\n\n" +
                "Das dauert mehrere Minuten und braucht vorübergehend ca. 20 GB freien Speicher. Fortfahren?", "Installieren"))
            return;
        var buildTask = _vm.InstallWiiCompiledAsync();
        var refresh = DispatcherQueue.CreateTimer();
        refresh.Interval = TimeSpan.FromSeconds(1);
        refresh.Tick += (_, _) => Build(keepFocus: true);
        refresh.Start();
        var error = await buildTask;
        refresh.Stop();
        Build();
        if (error != null)
            await Dialogs.MessageAsync("WiiCompiled", error);
        else
            MainWindow.Current.ShowToast("WiiCompiled ist installiert ✓");
    }

    private async Task InstallRetroRewindAsync()
    {
        if (!await Dialogs.ConfirmAsync("Retro Rewind",
                "Der Hub lädt keine Mods herunter. Lade das vollständige Retro-Rewind-Paket (ZIP) selbst von der " +
                "offiziellen Seite herunter und wähle es danach aus – es wird in den Dolphin-Userordner entpackt.",
                "ZIP wählen …"))
            return;
        var zip = await Dialogs.PickFileAsync(".zip");
        if (zip == null)
            return;
        var task = _vm.InstallRetroRewindAsync(zip);
        var refresh = DispatcherQueue.CreateTimer();
        refresh.Interval = TimeSpan.FromSeconds(1);
        refresh.Tick += (_, _) => Build(keepFocus: true);
        refresh.Start();
        var error = await task;
        refresh.Stop();
        Build();
        if (error != null)
            await Dialogs.MessageAsync("Retro Rewind", error);
    }

    private async Task ShowTracksAsync()
    {
        var layout = App.Hub.RetroRewind.Layout;
        if (layout == null)
        {
            await Dialogs.MessageAsync("Strecken", "Retro Rewind ist nicht installiert. Vanilla enthält die 32 Original-Strecken.");
            return;
        }
        var tracksDir = Path.Combine(layout.DataFolder, "Tracks");
        var count = Directory.Exists(tracksDir) ? Directory.EnumerateFiles(tracksDir, "*.szs", SearchOption.AllDirectories).Count() : 0;
        await Dialogs.MessageAsync("Strecken – Retro Rewind " + layout.Version,
            $"{count} Strecken-Dateien in Retro Rewind installiert.\n\nOrdner: {layout.DataFolder}\n\n" +
            "Eine Streckenliste mit Cup-Übersicht, Suche und Favoriten ist als spätere Erweiterung vorgesehen (Plan Abschnitt 7).");
    }

    private async Task ShowSettingsAsync()
    {
        var panel = new StackPanel { Spacing = 6, MinWidth = 420 };
        panel.Children.Add(Ui.Toggle("Retro Rewind: My-Stuff-Mods laden", _vm.MyStuff, v => _vm.MyStuff = v));
        panel.Children.Add(Ui.Toggle("Retro Rewind: getrennter Spielstand (Dolphin)", _vm.SeparateSave, v => _vm.SeparateSave = v));
        panel.Children.Add(Ui.Toggle("Spiel im Vollbild starten", App.Hub.Config.Current.Launch.EmulatorFullscreen,
            v => App.Hub.Config.Update(c => c.Launch.EmulatorFullscreen = v)));
        panel.Children.Add(Ui.Subtle("Diese Einstellungen gelten für den Start mit Dolphin. WiiCompiled speichert seine Optionen selbst (Config.toml)."));
        await Dialogs.ShowAsync("Mario Kart Wii – Einstellungen", panel, null, "Fertig");
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action)
    {
        switch (action)
        {
            case NavAction.Y:
                _ = PlayAsync();
                return true;
            case NavAction.X:
                _ = CheckUpdatesQuietlyAsync();
                MainWindow.Current.ShowToast("Retro-Rewind-Version wird geprüft …");
                return true;
            default:
                return false;
        }
    }
}
