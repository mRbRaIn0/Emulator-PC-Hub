using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Input;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Controller-Profil pro Spiel: Gerät je Spieler (automatisch nach Spieler-Platz oder fest), Modus
/// (Auto/Nativ/PadForge), emulierter Controller, Tastatur, Vibration, PadForge-Profil.
/// Zeigt direkt an, wie das Spiel gestartet würde.
/// </summary>
public sealed partial class ControllerProfilePage : Page, IHubPage
{
    private string _gameId = "default";
    private GameEntry? _game;
    /// <summary>Standardprofil: welches System in der Belegung gezeigt wird.</summary>
    private static EmulatedPad _overviewPad = EmulatedPad.Wiimote;

    public string Hints => "Ⓐ Ändern   Ⓑ Zurück";

    public ControllerProfilePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _gameId = e.Parameter as string ?? "default";
        _game = _gameId == "default" ? null : App.Hub.Library.Find(_gameId);
    }

    public void OnShown() => Build();

    private ControllerProfile Profile => App.Hub.Input.Profiles.For(_gameId);

    private string Backend => _game == null ? EmulatorIds.Dolphin : App.Hub.Input.BackendFor(_game);

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        var store = App.Hub.Input.Profiles;
        var own = _game != null && store.HasOwnProfile(_gameId);
        Body.Children.Add(Ui.Title(_game == null ? "Controller – Standardprofil" : $"Controller – {_game.DisplayTitle}"));
        Body.Children.Add(Ui.Subtle(_game == null
            ? "Gilt für alle Spiele ohne eigenes Profil."
            : own ? $"Eigenes Profil · Emulator: {BackendName(Backend)}" : $"Nutzt das Standardprofil · Emulator: {BackendName(Backend)}"));

        AddButtonMap();

        if (_game != null && !own)
        {
            Body.Children.Add(Ui.Buttons(Ui.Action("Eigenes Profil für dieses Spiel anlegen", "", () =>
            {
                store.GetOrCreate(_gameId);
                store.Save();
                Build();
            }, primary: true)));
            AddPreview();
            return;
        }

        var profile = _game == null ? store.Default : store.GetOrCreate(_gameId);
        for (int p = 1; p <= 4; p++)
            Body.Children.Add(PlayerEditor(profile, p));

        Body.Children.Add(Ui.Header("Optionen"));
        var padForgeName = new TextBox
        {
            Header = "PadForge-Profil (leer = „EmulatorPCHub“)",
            Text = profile.PadForgeProfile,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        padForgeName.LostFocus += (_, _) =>
        {
            profile.PadForgeProfile = padForgeName.Text.Trim();
            store.Save();
        };
        Body.Children.Add(Ui.Card(
            Ui.Toggle("Tastenbelegung in den Emulatoren automatisch setzen", profile.ManageMappings, v => { profile.ManageMappings = v; store.Save(); }),
            Ui.Toggle("Vibration", profile.Rumble, v => { profile.Rumble = v; store.Save(); }),
            Ui.Subtle("Ist die automatische Belegung aus, übergibt der Hub nur die Spielerreihenfolge nicht – du verwaltest die Belegung dann im Emulator selbst (Advanced)."),
            padForgeName));

        Body.Children.Add(Ui.Header("Tastatur"));
        var kb = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (label, value) in new[] { ("Aus", 0), ("Automatisch", -1), ("P1", 1), ("P2", 2), ("P3", 3), ("P4", 4) })
        {
            var b = Ui.Action(label, null, () => { profile.KeyboardPlayer = value; store.Save(); Build(keepFocus: true); });
            if (profile.KeyboardPlayer == value)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            kb.Children.Add(b);
        }
        Body.Children.Add(kb);

        if (_game != null)
        {
            Body.Children.Add(Ui.Buttons(Ui.Action("Eigenes Profil löschen (Standard verwenden)", "", () =>
            {
                store.Remove(_gameId);
                Build();
            })));
        }

        AddPreview();
        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private UIElement PlayerEditor(ControllerProfile profile, int player)
    {
        var store = App.Hub.Input.Profiles;
        var binding = profile.For(player);
        var pads = App.Hub.Input.Controllers;
        var fixedDevice = binding.DeviceKey == null ? null : pads.Devices.FirstOrDefault(d => d.Key == binding.DeviceKey);
        var deviceText = binding.DeviceKey switch
        {
            null => pads.ForPlayer(player) is { } cur ? $"Automatisch: wer auf Platz {player} ist (jetzt: {cur.Name})" : $"Automatisch: wer auf Platz {player} ist (jetzt: niemand)",
            "keyboard" => "Fest: Tastatur",
            _ => fixedDevice != null ? $"Fest: {fixedDevice.Name}" : "Fest: Gerät nicht verbunden",
        };

        // Auswahl: Automatisch → jedes verbundene Gerät (mit Spielerplatz, damit gleiche Controller unterscheidbar sind) → Tastatur
        var choices = new List<(string? key, string label)> { (null, $"Automatisch – wer auf Platz {player} ist (empfohlen)") };
        choices.AddRange(pads.Devices.Select(d => ((string?)d.Key,
            $"Fest: {d.Name}" + (d.Player > 0 ? $" (jetzt Spieler {d.Player})" : " (keinem Spieler zugewiesen)"))));
        choices.Add(("keyboard", "Fest: Tastatur"));

        var modeText = binding.Mode switch
        {
            PlayerMode.Native => "Nativ",
            PlayerMode.Compatibility => "PadForge",
            _ => "Automatisch",
        };
        var padOptions = PadOptions(Backend);
        var padText = binding.Pad == EmulatedPad.Auto ? "Automatisch" : PadName(binding.Pad);

        return Ui.Card(
            Ui.Text($"Spieler {player}", 18, bold: true),
            Ui.Subtle(deviceText),
            Ui.Buttons(
                Ui.AsyncAction("Gerät wählen …", "", async () =>
                {
                    var idx = await Dialogs.ChooseAsync($"Spieler {player}: Gerät", choices.Select(c => c.label).ToList(),
                        "Fest zugeordnete Geräte gelten nur für dieses Profil. Ein Gerät bekommt nie zwei Spieler – " +
                        "ist es schon vergeben, rückt das nächste freie Gerät nach.");
                    if (idx < 0)
                        return;
                    binding.DeviceKey = choices[idx].key;
                    // Dasselbe Gerät nicht zusätzlich bei einem anderen Spieler fest eintragen
                    if (binding.DeviceKey != null)
                        foreach (var other in profile.Players.Where(b => b.Player != player && b.DeviceKey == binding.DeviceKey))
                            other.DeviceKey = null;
                    store.Save();
                    MainWindow.Current.Sounds.Play(UiSound.Toggle);
                    Build(keepFocus: true);
                }),
                Ui.Action($"Modus: {modeText}", "", () =>
                {
                    binding.Mode = binding.Mode switch
                    {
                        PlayerMode.Auto => PlayerMode.Native,
                        PlayerMode.Native => PlayerMode.Compatibility,
                        _ => PlayerMode.Auto,
                    };
                    store.Save();
                    MainWindow.Current.Sounds.Play(UiSound.Toggle);
                    Build(keepFocus: true);
                }),
                Ui.Action($"Im Spiel: {padText}", "", () =>
                {
                    var idx = padOptions.IndexOf(binding.Pad);
                    binding.Pad = padOptions[(idx + 1) % padOptions.Count];
                    store.Save();
                    MainWindow.Current.Sounds.Play(UiSound.Toggle);
                    Build(keepFocus: true);
                })));
    }

    /// <summary>Belegung mit Grafik: welche Taste im Spiel was ist (für Spieler 1).</summary>
    private void AddButtonMap()
    {
        Body.Children.Add(Ui.Header("Tastenbelegung"));
        if (_game == null)
        {
            var systems = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var (label, systemPad) in new[] { ("Wii", EmulatedPad.Wiimote), ("GameCube", EmulatedPad.GameCube), ("Wii U", EmulatedPad.WiiUPro), ("Switch", EmulatedPad.SwitchPro) })
            {
                var b = Ui.Action(label, null, () =>
                {
                    _overviewPad = systemPad;
                    MainWindow.Current.Sounds.Play(UiSound.Toggle);
                    Build(keepFocus: true);
                });
                if (_overviewPad == systemPad)
                {
                    b.BorderBrush = Ui.AccentBrush;
                    b.BorderThickness = new Thickness(2);
                }
                systems.Children.Add(b);
            }
            Body.Children.Add(systems);
            Body.Children.Add(Ui.Card(ButtonMapView.Build(null, "default", _overviewPad, ControllerKind.PlayStation5, false, () => Build(keepFocus: true))));
            return;
        }
        if (Backend == EmulatorIds.WiiCompiled)
        {
            Body.Children.Add(Ui.Subtle("WiiCompiled ist ein PC-Port mit eigener Belegung – die Tasten stellst du im Spiel mit F10 ein."));
            return;
        }

        var setup = App.Hub.Input.Resolve(_game, Backend);
        var p1 = setup.Players.FirstOrDefault();
        var kind = p1?.Device.Kind ?? ControllerKind.PlayStation5;
        var pad = p1?.Pad ?? CompatibilityPolicy.DefaultPad(Backend, _game,
            new ControllerDescriptor { Key = "preview", Name = "PS5", Kind = ControllerKind.PlayStation5 });
        var realWiimote = p1 is { Mode: PlayerMode.Native } && kind == ControllerKind.WiiRemote && pad == EmulatedPad.Wiimote;

        var info = p1 == null
            ? $"Kein Controller verbunden – dargestellt für einen PS5-Controller als {ButtonMap.PadTitle(pad)}."
            : $"Spieler 1: {p1.Device.Name} → im Spiel {ButtonMap.PadTitle(pad)}" +
              (kind == ControllerKind.Keyboard ? " (Tastatur – Tastennamen siehe Controller-Seite, hier im PS5-Schema)" : "");
        if (!Profile.ManageMappings)
            info += "  ·  Automatische Belegung ist aus: Der Emulator nutzt deine eigene Belegung, die Tabelle zeigt die Hub-Belegung.";
        Body.Children.Add(Ui.Subtle(info));
        Body.Children.Add(Ui.Card(ButtonMapView.Build(_game, _gameId, pad, kind == ControllerKind.Keyboard ? ControllerKind.PlayStation5 : kind, realWiimote,
            () => Build(keepFocus: true))));
    }

    private void AddPreview()
    {
        Body.Children.Add(Ui.Header("So startet das Spiel"));
        var game = _game ?? new GameEntry { Id = "default", Title = "Standard", Platform = HubPlatform.Wii };
        var setup = App.Hub.Input.Resolve(game, Backend);
        var items = new List<UIElement>();
        if (setup.Players.Count == 0)
            items.Add(Ui.Status("Kein Controller und keine Tastatur zugeordnet", false));
        foreach (var p in setup.Players)
        {
            var mode = p.Mode == PlayerMode.Compatibility ? "über PadForge" : "nativ";
            items.Add(Ui.Status($"Spieler {p.Player}: {p.Device.Name} ({p.Device.Kind.DisplayName()}) → {PadName(p.Pad)}, {mode}", true));
            items.Add(Ui.Subtle("   " + p.Reason));
        }
        if (Backend == EmulatorIds.WiiCompiled)
            items.Add(Ui.Subtle("WiiCompiled übernimmt SDL-Controller direkt; die Feinbelegung erfolgt im Spiel mit F10."));
        Body.Children.Add(Ui.Card([.. items]));
    }

    private static List<EmulatedPad> PadOptions(string backend) => backend switch
    {
        EmulatorIds.Dolphin => [EmulatedPad.Auto, EmulatedPad.GameCube, EmulatedPad.Wiimote],
        EmulatorIds.Cemu => [EmulatedPad.Auto, EmulatedPad.WiiUPro],
        EmulatorIds.Switch => [EmulatedPad.Auto, EmulatedPad.SwitchPro, EmulatedPad.SwitchJoyConPair],
        EmulatorIds.MelonDS => [EmulatedPad.Auto, EmulatedPad.NintendoDS],
        EmulatorIds.Azahar => [EmulatedPad.Auto, EmulatedPad.Nintendo3DS],
        _ => [EmulatedPad.Auto],
    };

    private static string PadName(EmulatedPad pad) => pad switch
    {
        EmulatedPad.GameCube => "GameCube-Controller",
        EmulatedPad.Wiimote => "Wii Remote + Nunchuk",
        EmulatedPad.WiiUPro => "Wii U Pro Controller",
        EmulatedPad.WiiUGamePad => "Wii U GamePad",
        EmulatedPad.SwitchPro => "Switch Pro Controller",
        EmulatedPad.SwitchJoyConPair => "Joy-Con-Paar",
        EmulatedPad.NintendoDS => "Nintendo DS",
        EmulatedPad.Nintendo3DS => "Nintendo 3DS",
        _ => "Automatisch",
    };

    private static string BackendName(string id) => id switch
    {
        EmulatorIds.Dolphin => "Dolphin",
        EmulatorIds.Cemu => "Cemu",
        EmulatorIds.Switch => "Switch-Emulator",
        EmulatorIds.WiiCompiled => "WiiCompiled",
        EmulatorIds.MelonDS => "melonDS",
        EmulatorIds.Azahar => "Azahar",
        _ => id,
    };

    public bool HandleNav(NavAction action) => false;
}
