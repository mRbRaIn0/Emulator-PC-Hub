using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Controllers.Wii;
using EmulatorPCHub.UI.Services;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Controller-Verwaltung: automatische Erkennung (SDL3), Spieler 1–4 zuweisen/tauschen, Akkustand,
/// Tastatur, Profile pro Spiel und die optionale Kompatibilitätsschicht PadForge – alles direkt im Hub.
/// </summary>
public sealed partial class ControllersPage : Page, IHubPage
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _refresh;
    private string _padForgeText = "";
    /// <summary>Aufgeklappte Anleitungen (die Seite baut sich regelmäßig neu auf).</summary>
    private static readonly HashSet<string> OpenGuides = [];

    private static ControllerHub Pads => App.Hub.Input.Controllers;
    private static WiiRemoteMonitor Wiimotes => App.Hub.Input.Wiimotes;

    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _sensorTimer;
    private SensorView? _sensorView;
    private WiiRemoteSettings? _wiiSettings;
    private string _wiiSettingsText = "";
    /// <summary>Wii Remote (HID-Pfad) → Spieler in Wii-Spielen, bei jedem Aufbau neu berechnet.</summary>
    private IReadOnlyDictionary<string, int> _wiiPlayers = new Dictionary<string, int>();
    private string _signature = "";
    /// <summary>Während der Fokus nach einem Neuaufbau zurückgesetzt wird, darf die Seite nicht scrollen.</summary>
    private bool _restoringFocus;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Vibration testen   Ⓨ Beitreten";

    public ControllersPage()
    {
        InitializeComponent();
        _refresh = DispatcherQueue.CreateTimer();
        _refresh.Interval = TimeSpan.FromSeconds(2);
        _refresh.Tick += (_, _) =>
        {
            // Während des Sensortests nicht neu aufbauen – die Live-Anzeige läuft über _sensorTimer
            if (Wiimotes.SensorPath == null && Signature() != _signature)
                Build(keepFocus: true);
        };
        _sensorTimer = DispatcherQueue.CreateTimer();
        _sensorTimer.Interval = TimeSpan.FromMilliseconds(33);
        _sensorTimer.Tick += (_, _) => _sensorView?.Update(Wiimotes.LatestSample, Wiimotes.SensorState);
        Loaded += (_, _) =>
        {
            _refresh.Start();
            Wiimotes.Start();
        };
        Unloaded += (_, _) =>
        {
            _refresh.Stop();
            _sensorTimer.Stop();
            Wiimotes.StopSensorTest();
            Wiimotes.Stop();
        };
        Body.BringIntoViewRequested += (_, e) =>
        {
            if (_restoringFocus)
                e.Handled = true;
        };
        Pads.PlayersChanged += OnPlayersChanged;
        Unloaded += (_, _) => Pads.PlayersChanged -= OnPlayersChanged;
    }

    private void OnPlayersChanged() => DispatcherQueue.TryEnqueue(() =>
    {
        if (Signature() != _signature)
            Build(keepFocus: true);
    });

    /// <summary>
    /// Zustand, der die Anzeige verändert. Die Seite wird nur neu aufgebaut, wenn er sich ändert –
    /// sonst springen Fokus und Scrollposition alle 2 Sekunden.
    /// </summary>
    private string Signature()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Pads.JoinActive).Append('|').Append(Pads.SdlAvailable).Append('|');
        foreach (var d in Pads.Devices)
            sb.Append(d.Key).Append(':').Append(d.Player).Append(':').Append(d.BatteryPercent / 10).Append(d.Charging).Append(d.Wired).Append(';');
        sb.Append('|').Append(Wiimotes.HasScanned).Append(Wiimotes.BarMode).Append(Wiimotes.HubControlsLeds).Append('|');
        foreach (var r in Wiimotes.Remotes)
            sb.Append(r.Path).Append(':').Append(r.Responding).Append(r.Slot).Append(r.Status?.Leds).Append(r.Status?.BatteryPercent / 10)
              .Append(r.Status?.ExtensionConnected).Append(r.Extension).Append(';');
        var pf = App.Hub.Input.PadForge.Status();
        sb.Append('|').Append(pf.Installed).Append(pf.FirstRunDone).Append(pf.Running).Append(pf.ExternalControlEnabled);
        return sb.ToString();
    }

    public void OnShown()
    {
        _wiiSettings = null;
        Wiimotes.Start();
        Wiimotes.RefreshNow();
        Build();
    }

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        var offset = Scroller.VerticalOffset;
        Body.Children.Clear();
        _sensorView = null;
        UpdateWiiPlayers();
        _signature = Signature();

        Body.Children.Add(Ui.Title("Controller"));
        Body.Children.Add(Summary());

        if (Pads.JoinActive)
        {
            var join = Roomy(Ui.Card(
                Ui.Text("Beitreten: Drücke A (bzw. ✕) auf jedem Controller – in der Reihenfolge Spieler 1, 2, 3, 4.", 18, bold: true),
                Ui.Buttons(Ui.Action("Fertig", "", () => { Pads.EndJoin(); Build(); }, primary: true))));
            join.BorderBrush = Ui.AccentBrush;
            join.BorderThickness = new Thickness(2);
            join.Margin = new Thickness(0, 20, 0, 0);
            Body.Children.Add(join);
        }

        // ---- Spieler 1–4 ----
        Body.Children.Add(Section("Spieler", "Wer mit welchem Controller spielt – und was am Controller leuchtet."));
        var grid = new Grid { ColumnSpacing = 20, RowSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        var keyboardPlayer = App.Hub.Input.Profiles.Default.KeyboardPlayer;
        for (int p = 1; p <= 4; p++)
        {
            var card = PlayerCard(p, keyboardPlayer);
            Grid.SetColumn(card, (p - 1) % 2);
            Grid.SetRow(card, (p - 1) / 2);
            grid.Children.Add(card);
        }
        Body.Children.Add(grid);

        var playerActions = Ui.Buttons(
            Ui.Action("Neu zuordnen (Beitreten)", "", () => { Pads.BeginJoin(); Build(); }),
            Ui.Action("Automatisch sortieren", "", () => { Pads.AutoAssign(); Build(keepFocus: true); }),
            Ui.Action("Vibration testen", "", () => Pads.RumbleAll(0.6f, 350)));
        playerActions.Margin = new Thickness(0, 20, 0, 0);
        Body.Children.Add(playerActions);

        var unassigned = Pads.Devices.Where(d => d.Player == 0).ToList();
        if (unassigned.Count > 0)
        {
            Body.Children.Add(Section("Nicht zugewiesen", "Verbunden, aber noch keinem Spieler zugeordnet."));
            foreach (var d in unassigned)
            {
                var device = d;
                Body.Children.Add(Roomy(Ui.Card(
                    DeviceLine(device),
                    Ui.Buttons(
                        Ui.Action("Spieler zuweisen", "", () =>
                        {
                            var free = Enumerable.Range(1, 4).FirstOrDefault(x => Pads.ForPlayer(x) == null);
                            Pads.SetPlayer(device, free == 0 ? 1 : free);
                        }),
                        Ui.Action("Identifizieren", "", () => Pads.Identify(device))))));
            }
        }

        // ---- Tastatur ----
        Body.Children.Add(Section("Tastatur in Spielen", "Welchen Spielerplatz die Tastatur in Spielen bekommt."));
        var kb = new List<UIElement>();
        foreach (var (label, value) in new[] { ("Aus", 0), ("Automatisch", -1), ("Spieler 1", 1), ("Spieler 2", 2), ("Spieler 3", 3), ("Spieler 4", 4) })
        {
            var b = Ui.Action(label, null, () =>
            {
                App.Hub.Input.Profiles.Default.KeyboardPlayer = value;
                App.Hub.Input.Profiles.Save();
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                Build(keepFocus: true);
            });
            if (keyboardPlayer == value)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            kb.Add(b);
        }
        Body.Children.Add(Roomy(Ui.Card(
            Ui.Buttons([.. kb]),
            Ui.Subtle("Automatisch = Tastatur ist Spieler 1, wenn kein Controller angeschlossen ist."),
            Ui.Subtle("Belegung in allen Emulatoren gleich: Stick W/A/S/D · A=Z · B=X · X=C · Y=V · L=E · R=U · ZL=Q · ZR=O · " +
                      "+ = Enter · D-Pad = Pfeiltasten · rechter Stick I/J/K/L."))));

        BuildWii();

        // ---- Profile pro Spiel ----
        Body.Children.Add(Section("Tastenbelegung & Profil pro Spiel",
            "Pro Spiel: welche Taste welche Konsolen-Taste ist und was sie dort macht. Der Hub schreibt die Belegung beim Start selbst in den Emulator."));
        var games = App.Hub.Library.HomeOrder().ToList();
        var ownCount = games.Count(g => App.Hub.Input.Profiles.HasOwnProfile(g.Id));
        var gameButtons = games.Select(g =>
        {
            var game = g;
            var own = App.Hub.Input.Profiles.HasOwnProfile(game.Id);
            return (UIElement)Ui.Action($"{game.DisplayTitle}  ·  {game.Platform.ShortName()}" + (own ? "  ●" : ""), null,
                () => MainWindow.Current.Navigate(typeof(ControllerProfilePage), game.Id));
        }).ToArray();
        Body.Children.Add(Roomy(Ui.Card(
            Ui.Buttons(Ui.Action("Standardprofil (alle Spiele)", "", () => MainWindow.Current.Navigate(typeof(ControllerProfilePage), "default"), primary: true)),
            Expand("profiles", $"Spiele ({games.Count}, davon {ownCount} mit eigenem Profil ●)", Ui.Buttons(gameButtons)))));

        // ---- PadForge ----
        Body.Children.Add(Section("Kompatibilitätsschicht (PadForge)",
            "Nur für Controller, die ein Emulator nicht selbst kennt – z. B. eine Wii Remote im Switch-Emulator."));
        var pf = App.Hub.Input.PadForge.Status();
        var pfItems = new List<UIElement>
        {
            Ui.Status(pf.Installed ? $"PadForge {pf.Version} installiert" : "PadForge nicht installiert (Komponenten → PadForge)", pf.Installed),
        };
        if (pf.Installed)
        {
            pfItems.Add(Ui.Status(pf.FirstRunDone ? "Ersteinrichtung erledigt" : "Ersteinrichtung nötig (installiert den Treiber HIDMaestro, fragt einmal nach Admin-Rechten)", pf.FirstRunDone));
            pfItems.Add(Ui.Status(pf.Running ? "Läuft im Hintergrund" : "Wird bei Bedarf automatisch gestartet", true));
            pfItems.Add(Ui.Status(pf.ExternalControlEnabled ? "Profilsteuerung durch den Hub aktiv" : "Profilsteuerung wird beim Start eingeschaltet", pf.ExternalControlEnabled));
        }
        pfItems.Add(Ui.Toggle("PadForge automatisch verwenden, wenn ein Spiel einen Controller nicht direkt unterstützt",
            App.Hub.Input.Profiles.UseCompatibilityLayer, v => App.Hub.Input.Profiles.UseCompatibilityLayer = v));
        pfItems.Add(Ui.Subtle("Xbox-Controller laufen immer nativ. Sony- und Nintendo-Controller gehen nur dann über PadForge (virtueller Xbox-Controller), " +
                              "wenn der Emulator sie nicht selbst unterstützt."));
        pfItems.Add(pf.Installed
            ? Ui.Buttons(
                Ui.AsyncAction(pf.FirstRunDone ? "PadForge starten" : "PadForge einrichten", "", StartPadForgeAsync),
                Ui.Action("PadForge öffnen (Advanced)", "", () => App.Hub.Input.PadForge.Open()))
            : Ui.Buttons(Ui.Action("Zu Komponenten", "", () => MainWindow.Current.Navigate(typeof(DownloadsPage)))));
        if (!string.IsNullOrEmpty(_padForgeText))
            pfItems.Add(Ui.Subtle(_padForgeText));
        Body.Children.Add(Roomy(Ui.Card([.. pfItems])));

        // ---- Anleitungen ----
        Body.Children.Add(Section("Anleitungen", "Verbinden und Einrichten – zum Aufklappen."));
        Body.Children.Add(Guide("ps5", "PS5-Controller (DualSense)",
            "Kabel: per USB-C anstecken – fertig.",
            "Bluetooth: PS-Taste + Create ca. 3 s halten, bis die Lichtleiste schnell blinkt → Windows: Bluetooth → Gerät hinzufügen → „DualSense Wireless Controller“.",
            "Im Hub: ✕ = bestätigen, ○ = zurück. Im Spiel gilt die Tabelle unter „Tastenbelegung“ des Spiels.",
            "Bewegungssteuerung (Gyro) nutzen Cemu und Eden automatisch über SDL, z. B. zum Zielen."));
        Body.Children.Add(Guide("xbox", "Xbox-Controller",
            "Kabel oder Bluetooth (Pair-Taste oben am Controller halten, bis das Xbox-Logo schnell blinkt → Windows: Gerät hinzufügen).",
            "Läuft in allen Emulatoren direkt (XInput), die Belegung entspricht dem PS5-Controller nach Position."));
        Body.Children.Add(Guide("switch", "Switch Pro Controller / Joy-Con",
            "Sync-Knopf oben neben dem USB-C-Anschluss halten, bis die LEDs laufen → Windows: Gerät hinzufügen → „Pro Controller“. Alternativ per USB-Kabel.",
            "Joy-Con einzeln koppeln (Sync-Knopf an der Schiene); der Hub erkennt sie als Joy-Con-Paar für Switch-Spiele.",
            "Tasten gelten nach der Nintendo-Beschriftung: A rechts, B unten."));
        Body.Children.Add(Guide("wii", "Wii Remote mit der DolphinBar verbinden",
            "DolphinBar per USB direkt am PC anschließen und mittig über (oder unter) den Bildschirm legen.",
            "Mode 4 einstellen: die Mode-Taste an der DolphinBar drücken, bis die blaue LED bei „4“ leuchtet. Oben muss „Mode 4 (Dolphin)“ mit ✓ stehen.",
            "Wii Remote einschalten: 1 und 2 gleichzeitig drücken. Die LEDs blinken, dann erscheint sie hier unter „Wii-Remote-Verbindungen“ in Slot 1 – " +
            "die nächste Wii Remote in Slot 2 usw. Beim ersten Mal ggf. stattdessen den roten Sync-Knopf unter dem Batteriedeckel drücken.",
            "Die LED zeigt den Spieler: LED 1 = Spieler 1 … LED 4 = Spieler 4. Ist die Tastatur Spieler 1, wird die erste Wii Remote Spieler 2 – " +
            "„Tastatur in Spielen“ auf „Aus“ stellen, wenn du nur mit Wii Remotes spielst.",
            "„Identifizieren“ lässt eine Wii Remote kurz vibrieren; „Sensortest“ prüft Sensorleiste, Zeiger und Tasten.",
            "Nunchuk einfach einstecken – er erscheint als „Erweiterung: Nunchuk“. Später reicht zum Verbinden eine beliebige Taste bzw. 1+2.",
            "Wii-Spiel im Hub starten – Dolphin übernimmt die Wii Remotes automatisch (keine Windows-Kopplung nötig). Ausschalten: Power-Taste 3 s halten.",
            "Ohne DolphinBar: Windows → Bluetooth → Gerät hinzufügen → Bluetooth, roten Sync-Knopf drücken → „Nintendo RVL-CNT-01“ (PIN leer lassen). " +
            "Dein MediaTek-Bluetooth-Chip kann damit Probleme haben – die DolphinBar ist die zuverlässigere Lösung.",
            "Wii-U-Spiele mit Wii Remote (Cemu): in Cemu unter Optionen → Eingabeeinstellungen einen Controller auf „Wiimote“ mit API „Wiimote“ stellen."));
        Body.Children.Add(Guide("bar", "Sensorleiste / DolphinBar",
            "Die Sensorleiste sendet nur Infrarotlicht – die Kamera in der Wii Remote sieht zwei Punkte und berechnet daraus den Zeiger. Die DolphinBar hat diese LEDs eingebaut.",
            "Mode 1–3 machen die Wii Remote zu Maus/Tastatur/Gamepad für Windows; für Dolphin und den Hub immer Mode 4.",
            "Sensortest: Bei 2 Punkten funktioniert alles. 0 Punkte = Leiste ohne Strom, verdeckt oder falsch gezielt. Mehr als 2 = Störlicht (Sonne, Kerzen, Halogen).",
            "Unter „Einstellungen“ die Position (über/unter dem Bildschirm) passend zur Lage wählen und bei zitterndem Zeiger die Empfindlichkeit senken.",
            "Die originale Wii-Sensorleiste brauchst du mit DolphinBar nicht (ihr Stecker liefert nur Strom von der Wii).",
            "Ohne Wii Remote zeigst du mit dem rechten Stick deines Controllers (Hub-Belegung)."));
        Body.Children.Add(Guide("gamepad", "Wii U GamePad",
            "Das echte GamePad funkt über ein eigenes WLAN-Protokoll und lässt sich unter Windows nicht verbinden.",
            "Der Hub stellt deinen Controller in Wii-U-Spielen als Wii U Pro Controller ein – die Tasten sind dieselben wie am GamePad.",
            "GamePad-Bildschirm: in Cemu mit Strg+Tab einblenden, Touch-Eingaben mit der Maus.",
            "Braucht ein Spiel zwingend das GamePad: im Controller-Profil des Spiels „Tastenbelegung automatisch setzen“ ausschalten und in Cemu Controller 1 auf „Wii U GamePad“ stellen."));
        Body.Children.Add(Guide("gc", "GameCube-Controller",
            "Ohne Original-Controller: GameCube-Spiele belegt der Hub automatisch für PS5/Xbox (siehe Tastenbelegung im Spiel).",
            "Original-Controller: über den offiziellen Wii-U-GameCube-Adapter oder einen Mayflash-Adapter (Schalter auf „Wii U“).",
            "Einmalig den Treiber mit Zadig installieren: Gerät „WUP-028“ wählen → WinUSB → Install.",
            "In Dolphin unter Controller den Port auf „GameCube-Adapter für Wii U“ stellen und im Controller-Profil des Spiels „Tastenbelegung automatisch setzen“ ausschalten."));
        Body.Children.Add(Guide("kb", "Tastatur",
            "Stick W/A/S/D · A=Z · B=X · X=C · Y=V · L=E · R=U · ZL=Q · ZR=O · + = Enter · D-Pad = Pfeiltasten · rechter Stick I/J/K/L.",
            "Wer die Tastatur bekommt, stellst du oben unter „Tastatur in Spielen“ ein.",
            "Texteingaben: In Wii-Spielen mit USB-Tastatur-Unterstützung, in Cemu und Eden tippst du direkt mit der PC-Tastatur."));
        Body.Children.Add(Guide("ds", "Nintendo DS / 3DS (melonDS, Azahar)",
            "Der Hub belegt Spieler 1 automatisch nach Position: rechte Taste = A, untere = B, obere = X, linke = Y, L1/R1 = L/R; beim 3DS zusätzlich L2/R2 = ZL/ZR, linker Stick = Circle Pad, rechter Stick = C-Stick.",
            "DS: Das Steuerkreuz liegt zusätzlich auf dem linken Stick. Tastatur in melonDS: A=X · B=Z · X=S · Y=A · L=Q · R=W · Start=Enter · Select=Rücktaste · Steuerkreuz = Pfeiltasten.",
            "Den unteren Bildschirm (Touchscreen) bedienst du in beiden Emulatoren mit der Maus.",
            "DS und 3DS haben nur einen Spieler – weitere Controller im Profil werden ignoriert.",
            "3DS-Spiele als .3ds/.cci/.cxi (oder komprimiert .z3ds) ablegen; .cia-Dateien zuerst in Azahar über „Datei → CIA installieren“ einspielen."));

        // ---- Bedienung ----
        Body.Children.Add(Section("Bedienung im Hub", null));
        Body.Children.Add(Roomy(Ui.Card(
            Ui.Text("D-Pad / Stick = Navigation   ·   A = bestätigen   ·   B = zurück   ·   X/Y = Schnellaktionen"),
            Ui.Text("Start/+ = Optionen   ·   Home/Guide = Hub-Menü   ·   Home + Minus (1,5 s) = laufendes Spiel beenden (Xbox/XInput)"),
            Ui.Subtle("A/B richten sich nach der Beschriftung des Controllers: Bei Nintendo-Controllern ist A rechts, bei PlayStation ✕ = bestätigen."),
            Ui.Toggle("A/B tauschen", App.Hub.Config.Current.Ui.SwapConfirmButtons, v =>
            {
                App.Hub.Config.Update(c => c.Ui.SwapConfirmButtons = v);
                Pads.SwapConfirm = v;
            }),
            Ui.Toggle("Vibration", App.Hub.Config.Current.Ui.Vibration, v => App.Hub.Config.Update(c => c.Ui.Vibration = v)))));

        // ---- Bestätigen/Abbrechen in Spielen ----
        Body.Children.Add(Section("Bestätigen und Abbrechen in Spielen", null));
        Body.Children.Add(Roomy(Ui.Card(
            Ui.Text("Bei PlayStation- und Xbox-Controllern: ✕ / A (untere Taste) = bestätigen, ○ / B (rechte Taste) = zurück."),
            Ui.Subtle("Gilt für Wii-U-, Switch-, DS- und 3DS-Spiele in allen Emulatoren (Wii und GameCube bestätigen ohnehin mit ✕). " +
                      "Ausgeschaltet wird wie auf Nintendo-Konsolen nach Position belegt (rechte Taste = A). Nintendo-Controller bleiben unverändert."),
            Ui.Toggle("Bestätigen mit ✕ (untere Taste)", App.Hub.Input.Profiles.ConfirmWithSouth, v => App.Hub.Input.Profiles.ConfirmWithSouth = v))));

        if (keepFocus)
            RestoreFocusAndScroll(focus, offset);
    }

    // ------------------------------------------------------------------
    // DolphinBar, Wii Remotes, Sensortest
    // ------------------------------------------------------------------

    /// <summary>Spieler der Wii Remotes berechnen und die LEDs passend setzen lassen.</summary>
    private void UpdateWiiPlayers()
    {
        var remotes = Wiimotes.Remotes;
        Wiimotes.HubControlsLeds = Pads.Devices.All(d => d.Kind != ControllerKind.WiiRemote);
        _wiiPlayers = App.Hub.Input.WiiRemotePlayers(remotes);
        foreach (var r in remotes)
            Wiimotes.SetPlayer(r.Path, _wiiPlayers.TryGetValue(r.Path, out var p) ? p : 0);
    }

    private void BuildWii()
    {
        Body.Children.Add(Section("DolphinBar & Wii Remotes", "Status der Sensorleiste, verbundene Wii Remotes, Sensortest und Einstellungen."));
        var remotes = Wiimotes.Remotes;
        var responding = remotes.Where(r => r.Responding).ToList();

        // ---- Status der DolphinBar ----
        var bar = new List<UIElement>();
        if (!Wiimotes.HasScanned)
        {
            bar.Add(Ui.Status("Suche DolphinBar und Wii Remotes …", true));
        }
        else
        {
            bar.Add(Wiimotes.BarMode switch
            {
                DolphinBarMode.Dolphin => Ui.Status("DolphinBar erkannt – Mode 4 (Dolphin) · bereit für Wii-Spiele", true),
                DolphinBarMode.Gamepad => Ui.Status("DolphinBar erkannt, steht aber auf Mode 3 (Gamepad). Für Wii-Spiele und den Sensortest " +
                                                    "die Mode-Taste an der Leiste drücken, bis die blaue LED bei „4“ leuchtet.", false),
                DolphinBarMode.MouseKeyboard => Ui.Status("DolphinBar erkannt, steht aber auf Mode 1/2 (Maus/Tastatur). Mode-Taste drücken, " +
                                                          "bis die blaue LED bei „4“ leuchtet.", false),
                _ => Ui.Status(remotes.Any(r => !r.ViaDolphinBar)
                    ? "Keine DolphinBar angeschlossen – Wii Remotes laufen über Windows-Bluetooth"
                    : "Keine DolphinBar gefunden – USB-Kabel prüfen (am besten direkt am PC, nicht am USB-Hub)", false),
            });
            if (Wiimotes.BarMode != DolphinBarMode.None)
            {
                bar.Add(Ui.Status("Sensorleiste: Die Infrarot-LEDs der DolphinBar leuchten, solange sie USB-Strom hat. " +
                                  "Ob die Wii Remote sie sieht, zeigt der Sensortest unten.", true));
            }
        }
        bar.Add(Ui.Buttons(
            Ui.Action("Neu suchen", "", () => { Wiimotes.RefreshNow(); MainWindow.Current.Sounds.Play(UiSound.Toggle); }),
            Ui.Action("Anleitung", "", () => { OpenGuides.Add("wii"); OpenGuides.Add("bar"); Build(keepFocus: true); })));
        Body.Children.Add(Roomy(Ui.Card([.. bar])));

        // ---- Verbindungsübersicht ----
        var conn = new List<UIElement> { Ui.Text("Wii-Remote-Verbindungen", 18, bold: true) };
        if (Wiimotes.BarMode == DolphinBarMode.Dolphin)
        {
            for (var slot = 1; slot <= 4; slot++)
            {
                var s = slot;
                conn.Add(WiiRow($"Slot {slot}", responding.FirstOrDefault(x => x.ViaDolphinBar && x.Slot == s)));
            }
        }
        foreach (var r in responding.Where(x => !x.ViaDolphinBar))
            conn.Add(WiiRow("Bluetooth", r));
        if (Wiimotes.BarMode != DolphinBarMode.Dolphin && responding.All(x => x.ViaDolphinBar))
        {
            conn.Add(Ui.Subtle(Wiimotes.BarMode == DolphinBarMode.None
                ? "Keine Wii Remote verbunden."
                : "In Mode 1–3 kann der Hub die Wii Remotes nicht direkt sehen – erst Mode 4 zeigt Slots, Batterie und Spieler."));
        }

        conn.Add(Ui.Toggle("Wii-Spiele: freie Spielerplätze mit echten Wii Remotes belegen", App.Hub.Input.Profiles.RealWiimotesInFreeSlots, v =>
        {
            App.Hub.Input.Profiles.RealWiimotesInFreeSlots = v;
            Build(keepFocus: true);
        }));
        if (!Wiimotes.HubControlsLeds)
        {
            conn.Add(Ui.Subtle("Die Wii Remotes werden hier als Controller geführt – ihren Spieler tauschst du oben in den Spieler-Karten."));
        }
        else if (App.Hub.Input.Profiles.RealWiimotesInFreeSlots)
        {
            var free = App.Hub.Input.FreeWiiSlots();
            conn.Add(Ui.Subtle(free.Count > 0
                ? $"Freie Plätze in Wii-Spielen: Spieler {string.Join(", ", free)} – die Wii Remotes bekommen sie der Reihe nach (Slot 1 zuerst). " +
                  "Die LED an der Wii Remote zeigt schon jetzt ihren Spieler."
                : "Alle 4 Spielerplätze sind von anderen Controllern bzw. der Tastatur belegt – Wii Remotes werden nicht verwendet."));
            var kb = App.Hub.Input.Profiles.Default.KeyboardPlayer;
            var kbSlot = kb == -1 ? 1 : kb;
            if (kb != 0 && !free.Contains(kbSlot) && Pads.ForPlayer(kbSlot) == null && responding.Count > 0)
            {
                conn.Add(Ui.Status($"Die Tastatur belegt Spieler {kbSlot} – deshalb ist die erste Wii Remote Spieler " +
                                   $"{(free.Count > 0 ? free[0] : 0)}. Spielst du nur mit Wii Remotes, stell die Tastatur auf „Aus“.", false));
                conn.Add(Ui.Buttons(Ui.Action("Tastatur in Spielen: Aus", "", () =>
                {
                    App.Hub.Input.Profiles.Default.KeyboardPlayer = 0;
                    App.Hub.Input.Profiles.Save();
                    Build(keepFocus: true);
                })));
            }
        }
        else
        {
            conn.Add(Ui.Subtle("Aus: Wii-Spiele nutzen nur die oben zugewiesenen Controller."));
        }
        Body.Children.Add(Roomy(Ui.Card([.. conn])));

        // ---- Sensortest ----
        var sensorPath = Wiimotes.SensorPath;
        var test = new List<UIElement> { Ui.Text("Sensortest", 18, bold: true) };
        if (sensorPath != null)
        {
            var who = remotes.FirstOrDefault(r => r.Path.Equals(sensorPath, StringComparison.OrdinalIgnoreCase));
            test.Add(Ui.Subtle($"{who?.Label ?? "Wii Remote"}: Auf den Bildschirm zeigen und bewegen, Tasten drücken. Ⓑ beendet den Test."));
            _sensorView = new SensorView();
            _sensorView.Update(Wiimotes.LatestSample, Wiimotes.SensorState);
            test.Add(_sensorView.Root);
            test.Add(Ui.Buttons(Ui.Action("Test beenden", "", StopSensor, primary: true)));
        }
        else if (responding.Count > 0)
        {
            test.Add(Ui.Subtle("Prüft Sensorleiste, Zeiger, Tasten und Neigung live mit der Kamera der Wii Remote. Dolphin darf dabei nicht laufen."));
            test.Add(Ui.Buttons([.. responding.Select((r, i) => (UIElement)Ui.Action($"Sensortest · {r.Label}", "", () => StartSensor(r.Path), primary: i == 0))]));
        }
        else
        {
            test.Add(Ui.Subtle(Wiimotes.BarMode is DolphinBarMode.Gamepad or DolphinBarMode.MouseKeyboard
                ? "Für den Sensortest die DolphinBar auf Mode 4 stellen und eine Wii Remote verbinden (1+2)."
                : "Verbinde zuerst eine Wii Remote (DolphinBar Mode 4: an der Wii Remote 1+2 drücken)."));
        }
        Body.Children.Add(Roomy(Ui.Card([.. test])));

        // ---- Einstellungen (Wii-Systemeinstellungen in Dolphin) ----
        _wiiSettings ??= SafeRead();
        var st = _wiiSettings;
        var set = new List<UIElement>();
        if (st == null || !st.Available)
        {
            set.Add(Ui.Status("Die Wii-Systemeinstellungen von Dolphin fehlen noch – einmal ein Wii-Spiel starten, dann sind sie hier einstellbar.", false));
        }
        else
        {
            set.Add(Ui.Subtle("Position der Sensorleiste"));
            set.Add(Choices([("Über dem Bildschirm", 1), ("Unter dem Bildschirm", 0)], st.SensorBarTop ? 1 : 0,
                v => SaveWii(st with { SensorBarPosition = v })));
            set.Add(Ui.Subtle("Empfindlichkeit (1 = gering … 5 = hoch). Zittert oder springt der Zeiger: niedriger. Verliert er die Leiste aus großer Entfernung: höher."));
            set.Add(Choices([("1", 1), ("2", 2), ("3 (Standard)", 3), ("4", 4), ("5", 5)], st.Sensitivity, v => SaveWii(st with { Sensitivity = v })));
            set.Add(Ui.Toggle("Vibration der Wii Remote", st.Rumble, v => SaveWii(st with { Rumble = v })));
            set.Add(Ui.Toggle("Lautsprecher der Wii Remote (kann auf manchen PCs ruckeln)", st.RealSpeaker, v => SaveWii(st with { RealSpeaker = v })));
            if (st.RealSpeaker)
            {
                set.Add(Ui.Subtle("Lautstärke"));
                set.Add(Choices([("Leise", 40), ("Mittel", 88), ("Laut", 127)], st.SpeakerVolume <= 60 ? 40 : st.SpeakerVolume <= 105 ? 88 : 127,
                    v => SaveWii(st with { SpeakerVolume = v })));
            }
            set.Add(Ui.Subtle("Gilt für alle Wii-Spiele in Dolphin (Wii-Systemeinstellungen). Dolphin muss beim Ändern geschlossen sein."));
        }
        if (!string.IsNullOrEmpty(_wiiSettingsText))
            set.Add(Ui.Status(_wiiSettingsText, _wiiSettingsText.StartsWith("Gespeichert")));
        var setPanel = new StackPanel { Spacing = 12 };
        foreach (var e in set)
            setPanel.Children.Add(e);
        Body.Children.Add(Roomy(Ui.Card(Expand("wiisettings", "Sensorleiste & Wii Remote – Einstellungen", setPanel))));
    }

    private UIElement WiiRow(string label, WiiRemoteInfo? r)
    {
        var grid = new Grid { ColumnSpacing = 20, Padding = new Thickness(0, 8, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var head = Ui.Text(label, 15, bold: true);
        head.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(head);

        FrameworkElement middle;
        if (r == null)
        {
            middle = Ui.Subtle("leer – an der Wii Remote 1+2 drücken");
        }
        else
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(WiiRemoteLine(r));
            var player = _wiiPlayers.TryGetValue(r.Path, out var p) ? p : 0;
            var leds = r.Status?.Leds ?? 0;
            panel.Children.Add(PlayerLight.WiiLeds(leds, player > 0
                ? $"Spieler {player} · LED {player} leuchtet blau"
                : leds == 0 ? "kein Spieler (LEDs aus)" : "kein Spieler zugewiesen"));
            middle = panel;
        }
        middle.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(middle, 1);
        grid.Children.Add(middle);

        if (r != null)
        {
            var path = r.Path;
            var buttons = Ui.Buttons(
                Ui.Action("Identifizieren", "", () => Wiimotes.Identify(path)),
                Ui.Action("Sensortest", "", () => StartSensor(path)));
            Grid.SetColumn(buttons, 2);
            grid.Children.Add(buttons);
        }
        return grid;
    }

    private static UIElement WiiRemoteLine(WiiRemoteInfo r)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Ui.Text((r.IsPlus ? "Wii Remote Plus" : "Wii Remote") + (r.ViaDolphinBar ? $" · DolphinBar-Slot {r.Slot}" : " · Bluetooth"), 16, bold: true));
        var parts = new List<string>();
        if (r.Status is { } s)
        {
            parts.Add($"Batterie ca. {s.BatteryPercent} %{(s.BatteryLow ? " – bald wechseln!" : "")}");
            parts.Add("Erweiterung: " + (s.ExtensionConnected ? r.Extension.DisplayName() : "keine"));
        }
        else
        {
            parts.Add("antwortet nicht");
        }
        panel.Children.Add(Ui.Subtle(string.Join(" · ", parts)));
        return panel;
    }

    private void StartSensor(string path)
    {
        if (System.Diagnostics.Process.GetProcessesByName("Dolphin").Length > 0)
        {
            _wiiSettingsText = "Sensortest nicht möglich, solange Dolphin läuft (Dolphin hat die Wii Remotes).";
            Build(keepFocus: true);
            return;
        }
        _wiiSettingsText = "";
        Wiimotes.StartSensorTest(path);
        _sensorTimer.Start();
        MainWindow.Current.Sounds.Play(UiSound.Toggle);
        Build();
        _sensorView?.Root.StartBringIntoView();
    }

    private void StopSensor()
    {
        Wiimotes.StopSensorTest();
        _sensorTimer.Stop();
        Wiimotes.RefreshNow();
        Build(keepFocus: true);
    }

    private static WiiRemoteSettings? SafeRead()
    {
        try
        {
            return App.Hub.Input.ReadWiiSettings();
        }
        catch (Exception ex)
        {
            EmulatorPCHub.Core.Logging.HubLog.Warn("Wii-Einstellungen konnten nicht gelesen werden", ex);
            return null;
        }
    }

    private void SaveWii(WiiRemoteSettings s)
    {
        try
        {
            var error = App.Hub.Input.WriteWiiSettings(s);
            _wiiSettingsText = error ?? "Gespeichert ✓ – gilt ab dem nächsten Start eines Wii-Spiels.";
        }
        catch (Exception ex)
        {
            _wiiSettingsText = "Speichern fehlgeschlagen: " + ex.Message;
        }
        _wiiSettings = null;
        MainWindow.Current.Sounds.Play(UiSound.Toggle);
        Build(keepFocus: true);
    }

    /// <summary>Knopfreihe zur Auswahl eines Werts (ausgewählt = Akzentrahmen), wie bei „Tastatur in Spielen“.</summary>
    private static WrapPanelLike Choices((string Label, int Value)[] options, int current, Action<int> onSelect)
    {
        var buttons = options.Select(o =>
        {
            var b = Ui.Action(o.Label, null, () => onSelect(o.Value));
            if (o.Value == current)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            return (UIElement)b;
        }).ToArray();
        return Ui.Buttons(buttons);
    }

    // ------------------------------------------------------------------
    // Layout-Bausteine
    // ------------------------------------------------------------------

    /// <summary>Abschnittsüberschrift mit mehr Luft nach oben und optionaler Erklärung.</summary>
    private static StackPanel Section(string title, string? subtitle)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 40, 0, 14) };
        var header = Ui.Header(title);
        header.Margin = new Thickness(0);
        panel.Children.Add(header);
        if (subtitle != null)
            panel.Children.Add(Ui.Subtle(subtitle));
        return panel;
    }

    /// <summary>Karte mit mehr Innenabstand und Zeilenabstand.</summary>
    private static Border Roomy(Border card)
    {
        card.Padding = new Thickness(26, 22, 26, 22);
        card.Margin = new Thickness(0, 0, 0, 18);
        if (card.Child is StackPanel sp)
            sp.Spacing = 14;
        return card;
    }

    /// <summary>Aufklappbarer Bereich, dessen Zustand Neuaufbauten übersteht.</summary>
    private static Expander Expand(string id, string title, UIElement content)
    {
        var expander = new Expander
        {
            Header = Ui.Text(title, 16, bold: true),
            Content = content,
            IsExpanded = OpenGuides.Contains(id),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        expander.Expanding += (_, _) => OpenGuides.Add(id);
        expander.Collapsed += (_, _) => OpenGuides.Remove(id);
        return expander;
    }

    /// <summary>Kurzübersicht oben: Controller, DolphinBar, Wii Remotes, Tastatur, PadForge.</summary>
    private UIElement Summary()
    {
        var chips = new List<UIElement>();
        void Chip(string text, bool ok)
        {
            chips.Add(new Border
            {
                Padding = new Thickness(14, 6, 14, 7),
                Margin = new Thickness(0, 0, 10, 10),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                BorderBrush = ok ? Ui.SubtleBrush : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HubWarnBrush"],
                Child = Ui.Text((ok ? "✓ " : "! ") + text, 14),
            });
        }
        if (Pads.SdlAvailable)
            Chip($"{Pads.ConnectedCount} Controller verbunden", Pads.ConnectedCount > 0);
        else
            Chip($"SDL3 nicht verfügbar – nur Xbox/XInput ({Pads.SdlError})", false);
        if (Wiimotes.HasScanned)
        {
            Chip(Wiimotes.BarMode switch
            {
                DolphinBarMode.Dolphin => "DolphinBar Mode 4",
                DolphinBarMode.Gamepad => "DolphinBar auf Mode 3 – bitte Mode 4",
                DolphinBarMode.MouseKeyboard => "DolphinBar auf Mode 1/2 – bitte Mode 4",
                _ => "Keine DolphinBar",
            }, Wiimotes.BarMode is DolphinBarMode.Dolphin or DolphinBarMode.None);
            var wii = Wiimotes.Remotes.Count(r => r.Responding);
            if (wii > 0 || Wiimotes.BarMode == DolphinBarMode.Dolphin)
                Chip($"{wii} Wii Remote{(wii == 1 ? "" : "s")}", true);
        }
        var kb = App.Hub.Input.Profiles.Default.KeyboardPlayer;
        Chip(kb switch { 0 => "Tastatur aus", -1 => "Tastatur automatisch", _ => $"Tastatur = Spieler {kb}" }, true);
        var pf = App.Hub.Input.PadForge.Status();
        Chip(pf.Installed ? (pf.FirstRunDone ? "PadForge bereit" : "PadForge einrichten") : "PadForge nicht installiert", pf.Installed && pf.FirstRunDone);
        var wrap = Ui.Buttons([.. chips]);
        wrap.Margin = new Thickness(0, 6, 0, 0);
        return wrap;
    }

    /// <summary>Fokus nach dem Neuaufbau zurücksetzen, ohne dass die Seite dabei scrollt.</summary>
    private void RestoreFocusAndScroll(int focus, double offset)
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            Scroller.ChangeView(null, offset, null, disableAnimation: true);
            if (focus >= 0)
            {
                var list = FocusKeeper.Focusables(this);
                if (list.Count > 0)
                {
                    _restoringFocus = true;
                    try { list[Math.Min(focus, list.Count - 1)].Focus(FocusState.Keyboard); }
                    finally { _restoringFocus = false; }
                }
            }
        });
    }

    private static Expander Guide(string id, string title, params string[] steps)
    {
        var list = new StackPanel { Spacing = 8 };
        for (var i = 0; i < steps.Length; i++)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var text = Ui.Text(steps[i], 15);
            text.TextWrapping = TextWrapping.Wrap;
            Grid.SetColumn(text, 1);
            row.Children.Add(Ui.Text($"{i + 1}.", 15, bold: true));
            row.Children.Add(text);
            list.Children.Add(row);
        }
        var expander = new Expander
        {
            Header = Ui.Text(title, 17, bold: true),
            Content = list,
            IsExpanded = OpenGuides.Contains(id),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 12),
        };
        expander.Expanding += (_, _) => OpenGuides.Add(id);
        expander.Collapsed += (_, _) => OpenGuides.Remove(id);
        return expander;
    }

    private Border PlayerCard(int player, int keyboardPlayer)
    {
        var device = Pads.ForPlayer(player);
        var items = new List<UIElement> { Ui.Text($"Spieler {player}", 20, bold: true) };
        if (device == null)
        {
            var kbHere = keyboardPlayer == player || (keyboardPlayer == -1 && player == 1 && Pads.ForPlayer(1) == null);
            var wii = Wiimotes.Remotes.FirstOrDefault(r => r.Responding && _wiiPlayers.TryGetValue(r.Path, out var wp) && wp == player);
            if (kbHere)
                items.Add(Ui.Subtle("Tastatur"));
            if (wii != null)
            {
                items.Add(WiiRemoteLine(wii));
                items.Add(PlayerLight.For(ControllerKind.WiiRemote, player));
                items.Add(Ui.Subtle(kbHere ? "Wii-Spiele: Tastatur hat Vorrang – Wii Remote rückt auf den nächsten freien Platz"
                                           : "in Wii-Spielen (Dolphin)"));
                items.Add(Ui.Buttons(Ui.Action("Identifizieren", "", () => Wiimotes.Identify(wii.Path))));
            }
            else if (!kbHere)
            {
                items.Add(Ui.Subtle("frei"));
            }
        }
        else
        {
            items.Add(DeviceLine(device));
            items.Add(PlayerLight.For(device.Kind, player));
            items.Add(Ui.Buttons(
                Ui.Action("◀", null, () => Move(device, -1)),
                Ui.Action("▶", null, () => Move(device, +1)),
                Ui.Action("Identifizieren", "", () => Pads.Identify(device)),
                Ui.Action("Entfernen", "", () => Pads.SetPlayer(device, 0))));
        }
        var card = Roomy(Ui.Card([.. items]));
        card.Margin = new Thickness(0);
        card.MinHeight = 170;
        if (device != null || items.Count > 2)
        {
            card.BorderBrush = Ui.AccentBrush;
            card.BorderThickness = new Thickness(2);
        }
        return card;
    }

    private static UIElement DeviceLine(ControllerDevice d)
    {
        var connection = d.Wired ? "Kabel" : "Kabellos";
        var battery = d.BatteryPercent is { } b ? $"Akku {b} %{(d.Charging ? " (lädt)" : "")}" : d.Wired ? "" : "Akku unbekannt";
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(Ui.Text($"{d.Name}", 16, bold: true));
        panel.Children.Add(Ui.Subtle($"{d.Kind.DisplayName()} · {connection}{(battery.Length > 0 ? " · " + battery : "")}"));
        if (d.BatteryPercent is { } p)
        {
            panel.Children.Add(new ProgressBar
            {
                Value = p,
                Maximum = 100,
                Width = 160,
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = p <= 15 ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HubWarnBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HubGreenBrush"],
            });
        }
        return panel;
    }

    private static void Move(ControllerDevice d, int delta)
    {
        var target = Math.Clamp(d.Player + delta, 1, 4);
        if (target != d.Player)
        {
            Pads.SetPlayer(d, target);
            MainWindow.Current.Sounds.Play(UiSound.Toggle);
        }
    }

    private async Task StartPadForgeAsync()
    {
        var status = App.Hub.Input.PadForge.Status();
        if (!status.FirstRunDone && !await Dialogs.ConfirmAsync("PadForge einrichten",
                "PadForge läuft immer mit Administratorrechten – Windows fragt gleich einmal nach (UAC).\n\n" +
                "Beim ersten Start installiert PadForge den Treiber HIDMaestro für virtuelle Controller. Danach startet der Hub " +
                "PadForge bei Bedarf automatisch im Hintergrund und schaltet die Profile selbst um.\n\nJetzt starten?", "Starten"))
            return;
        _padForgeText = "PadForge wird gestartet …";
        Build(keepFocus: true);
        var ok = await App.Hub.Input.PadForge.EnsureRunningAsync();
        _padForgeText = ok
            ? "PadForge läuft und ist mit dem Hub verbunden ✓ Weise dort einmal deine Controller einem virtuellen Xbox-Slot zu (Add Controller)."
            : "PadForge antwortet noch nicht. Falls ein Fenster offen ist: Ersteinrichtung abschließen und unter Profiles „Allow External Control“ einschalten.";
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action)
    {
        if (action == NavAction.Back && Wiimotes.SensorPath != null)
        {
            StopSensor();
            return true;
        }
        switch (action)
        {
            case NavAction.X:
                Pads.RumbleAll(0.6f, 350);
                return true;
            case NavAction.Y:
                Pads.BeginJoin();
                Build();
                return true;
            default:
                return false;
        }
    }
}
