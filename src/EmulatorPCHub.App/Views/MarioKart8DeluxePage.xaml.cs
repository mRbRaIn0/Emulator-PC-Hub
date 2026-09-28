using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Mario-Kart-8-Deluxe-Seite (Plan Abschnitt 8): Edition Vanilla / CTGP Deluxe / Custom,
/// CTGP Deluxe wirkt wie eine eigene Edition. Eigene Updates und DLCs werden automatisch eingetragen.
/// </summary>
public sealed partial class MarioKart8DeluxePage : Page, IHubPage
{
    private readonly MarioKart8DeluxeViewModel _vm = new(App.Hub);
    private Button? _playButton;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   Ⓨ Spielen";

    public MarioKart8DeluxePage()
    {
        InitializeComponent();
    }

    public void OnShown()
    {
        _vm.Reload();
        Build();
        MainWindow.Current.SetBackdrop("#FFE53935", "#FF7A0E12", _vm.Game.BackgroundPath ?? _vm.Game.CoverPath);
    }

    private void Build(bool keepFocus = false)
    {
        var focusIndex = keepFocus ? FocusKeeper.Capture(this) : -1;
        Left.Children.Clear();
        Right.Children.Clear();

        var selected = _vm.SelectedPreset;
        Left.Children.Add(Ui.Title("Mario Kart 8 Deluxe"));
        Left.Children.Add(Ui.Subtle($"{selected.Name}   ·   Spielzeit: {_vm.PlayTimeText}   ·   {_vm.ReadyText}"));

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

        if (_vm.ShowCustomMods)
        {
            Left.Children.Add(Ui.Header("Eigene Mod-Konfiguration"));
            if (_vm.CustomMods.Count == 0)
                Left.Children.Add(Ui.Subtle("Noch keine Mods vorhanden. Mods-Seite → Mod importieren."));
            foreach (var mod in _vm.CustomMods)
            {
                var m = mod;
                Left.Children.Add(Ui.Toggle($"{m.Name} {m.VersionText}", m.IsChecked, v =>
                {
                    m.IsChecked = v;
                    _vm.SaveCustomMods();
                }));
            }
        }

        if (selected.Id == "mk8dx-ctgp")
        {
            Left.Children.Add(Ui.Card(
                Ui.Text("CTGP Deluxe", 20, bold: true),
                Ui.Text("14 zusätzliche Cups · 56 Custom Tracks · neuer Soundtrack"),
                Ui.Subtle($"{_vm.CtgpVersionText}   ·   {_vm.CtgpText}"),
                Ui.Subtle(_vm.CompatText)));
        }

        // ---- Rechts: Status ----
        Right.Children.Add(Ui.Header("Status"));
        Right.Children.Add(Ui.Card(
            Ui.Status(_vm.GameText, _vm.HasGame),
            Ui.Status(_vm.EmulatorText, !_vm.EmulatorText.Contains("nicht installiert")),
            Ui.Status(_vm.KeysText, _vm.KeysText.Contains('✓')),
            Ui.Status(_vm.FirmwareText, _vm.FirmwareText.Contains('✓')),
            Ui.Status(_vm.CtgpText + (_vm.CtgpVersionText.Length > 0 ? $" ({_vm.CtgpVersionText})" : ""), !_vm.CtgpText.Contains("nicht")),
            Ui.Status(_vm.UpdateText, !_vm.UpdateText.StartsWith("Kein")),
            Ui.Status(_vm.DlcText, !_vm.DlcText.StartsWith("Keine"))));
        if (!string.IsNullOrEmpty(_vm.CompatText))
            Right.Children.Add(Ui.Subtle(_vm.CompatText));

        Right.Children.Add(Ui.Header("Einrichtung"));
        Right.Children.Add(Ui.Buttons(
            Ui.AsyncAction(_vm.HasGame ? "Anderen Dump wählen" : "Eigenen Dump wählen", "", ChooseGameAsync),
            Ui.Action("DLC & Updates eintragen", "", () =>
            {
                MainWindow.Current.ShowToast(_vm.RegisterAddOns());
                Build(keepFocus: true);
            }),
            Ui.AsyncAction("CTGP Deluxe importieren (ZIP)", "", ImportCtgpAsync),
            Ui.AsyncAction("Switch-Emulator einrichten (Keys, Firmware)", "\uE713", SetupEmulatorAsync),
            Ui.Action("Controller-Profil", "\uE7FC", () => MainWindow.Current.Navigate(typeof(ControllerProfilePage), _vm.Game.Id)),
            Ui.Action("Switch-Emulator-Ordner", "\uE838", () =>
                SystemService.OpenFolder(App.Hub.Adapters.Switch.DataDirectory()))));
        if (ControllerAdviceView.For(_vm.Game) is { } advice)
        {
            Right.Children.Add(Ui.Header("Controller-Empfehlung"));
            Right.Children.Add(ControllerAdviceView.Details(advice));
        }
        if (_vm.IsBusy)
        {
            Right.Children.Add(new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0, 6, 0, 6) });
            Right.Children.Add(Ui.Subtle(_vm.BusyText));
        }

        Right.Children.Add(Ui.Card(
            Ui.Text("Eigene Spielkopie, Keys, Firmware & DLCs", 15, bold: true),
            Ui.Subtle("Der Hub lädt keine Spiele, Keys, Firmware oder DLCs herunter. Nutze deine eigenen Dumps: " +
                      "Spiel (NSP/XCI) und DLCs/Updates (NSP) in den Switch-Bibliotheksordner legen – der Hub erkennt " +
                      "Updates und DLCs (z. B. Booster-Streckenpass) automatisch und trägt sie im Emulator ein. " +
                      "prod.keys gehört nach …\\user\\keys\\, die Firmware wird in Eden über Tools → Install Firmware eingespielt.")));

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
        _vm.Reload();
        Build(keepFocus: true);
    }

    private async Task SetupEmulatorAsync()
    {
        if (!App.Hub.Adapters.Switch.DetectInstallation().IsInstalled)
        {
            await Dialogs.MessageAsync("Switch-Emulator", "Eden ist noch nicht installiert. Lade Eden selbst von der offiziellen Seite " +
                                                          "(git.eden-emu.dev) und installiere es unter Komponenten → Eden → „Aus Datei installieren …“ (Datei " +
                                                          "Eden-Windows-…-amd64-clang-pgo.zip).");
            return;
        }
        if (!await Dialogs.ConfirmAsync("Switch-Emulator einrichten",
                "Der Switch-Emulator öffnet sich jetzt einmal normal. Dort:\n\n" +
                "1. Eigene prod.keys einspielen (Tools → Install Decryption Keys)\n" +
                "2. Eigene Firmware installieren (Tools → Install Firmware)\n\n" +
                "Danach das Fenster schließen – der Hub trägt dann Spieleordner, Updates/DLCs und die Controller-Belegung selbst ein.",
                "Öffnen"))
            return;
        var result = await App.Hub.RunSwitchEmulatorSetupAsync(MainWindow.Current.WindowService);
        _vm.Reload();
        Build();
        await Dialogs.MessageAsync("Switch-Emulator", result);
    }

    private async Task ChooseGameAsync()
    {
        var file = await Dialogs.PickFileAsync(".nsp", ".xci", ".nsz", ".xcz");
        if (file == null)
            return;
        var error = _vm.SetGameFile(file);
        if (error != null)
            await Dialogs.MessageAsync("Datei nicht passend", error);
        Build();
    }

    private async Task ImportCtgpAsync()
    {
        var file = await Dialogs.PickFileAsync(".zip");
        if (file == null)
            return;
        var task = _vm.ImportCtgpAsync(file);
        Build(keepFocus: true);
        var error = await task;
        Build();
        if (error != null)
            await Dialogs.MessageAsync("CTGP Deluxe", error);
        else
            MainWindow.Current.ShowToast("CTGP Deluxe importiert ✓");
    }

    public bool HandleNav(NavAction action)
    {
        if (action == NavAction.Y)
        {
            _ = PlayAsync();
            return true;
        }
        return false;
    }
}
