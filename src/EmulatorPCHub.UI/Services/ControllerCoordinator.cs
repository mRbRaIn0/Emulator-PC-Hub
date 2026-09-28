using EmulatorPCHub.Controllers;
using EmulatorPCHub.Controllers.Wii;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.Input;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.UI.Services;

/// <summary>
/// Verbindet Controller-Verwaltung, Profile pro Spiel, PadForge und die Emulator-Konfiguration:
/// Vor dem Start werden die Spieler aufgelöst, bei Bedarf PadForge aktiviert und die Belegung
/// in Dolphin/Cemu/Eden geschrieben – der Nutzer muss kein Controller-Programm selbst öffnen.
/// </summary>
public sealed class ControllerCoordinator
{
    public const string DefaultPadForgeProfile = "EmulatorPCHub";

    private readonly HubServices _hub;
    private bool _padForgeActive;

    public ControllerHub Controllers { get; } = new();
    /// <summary>Wii Remotes/DolphinBar direkt über HID (nur solange die Controller-Seite offen ist).</summary>
    public WiiRemoteMonitor Wiimotes { get; } = new();
    public PadForgeService PadForge { get; }
    public ControllerProfileStore Profiles { get; }
    /// <summary>Was die Tasten im jeweiligen Spiel machen (für die Belegungsanzeige).</summary>
    public GameControlsStore GameControls { get; }

    public ControllerCoordinator(HubServices hub)
    {
        _hub = hub;
        PadForge = new PadForgeService(hub.Paths);
        Profiles = new ControllerProfileStore(hub.Paths);
        GameControls = new GameControlsStore(hub.Paths);
        Controllers.LoadAssignment = Profiles.GetAssignment;
        Controllers.SaveAssignment = Profiles.SetAssignment;
    }

    /// <summary>Welches Backend (für die Kompatibilitätsentscheidung) ein Spiel nutzt.</summary>
    public string BackendFor(GameEntry game) => _hub.AdapterFor(game, _hub.Presets.Active(game)).Id;

    /// <summary>Löst die Spieler für ein Spiel auf (auch für die Anzeige im Hub).</summary>
    public InputSetup Resolve(GameEntry game, string backendId)
    {
        var profile = Profiles.For(game.Id);
        var setup = new InputSetup
        {
            ManageMappings = profile.ManageMappings,
            Rumble = profile.Rumble,
            RealWiimotesInFreeSlots = Profiles.RealWiimotesInFreeSlots && game.Platform == HubPlatform.Wii,
            Remap = profile.Remap.ToList(),
        };
        var slots = Controllers.SdlAvailable
            ? Controllers.Devices.Where(d => d.Player > 0).ToDictionary(d => d.Player, Controllers.Describe)
            : Controllers.DescribeAll().Select((d, i) => (d, i)).ToDictionary(x => x.i + 1, x => x.d);
        var connected = Controllers.SdlAvailable ? Controllers.Devices.Select(Controllers.Describe).ToList() : slots.Values.ToList();
        var assigned = AssignDevices(profile, slots, connected);
        var padForgeInstalled = PadForge.ExePath != null && Profiles.UseCompatibilityLayer;

        for (int p = 1; p <= 4; p++)
        {
            var binding = profile.For(p);
            if (!assigned.TryGetValue(p, out var device))
                continue;

            var (mode, reason) = CompatibilityPolicy.Decide(backendId, device.Kind, binding.Mode);
            if (mode == PlayerMode.Compatibility && !padForgeInstalled)
            {
                mode = PlayerMode.Native;
                reason += " – PadForge ist nicht installiert/aktiv, Gerät wird direkt übergeben";
            }
            var pad = binding.Pad == EmulatedPad.Auto ? CompatibilityPolicy.DefaultPad(backendId, game, device) : binding.Pad;
            setup.Players.Add(new ResolvedPlayer(p, device, mode, pad, reason));
        }
        return setup;
    }

    /// <summary>
    /// Geräte je Spieler: feste Zuordnungen des Profils zuerst, dann die Spieler-Plätze. Jedes Gerät bekommt höchstens
    /// einen Spieler – liegt ein fest zugeordnetes Gerät auf einem anderen Platz, rückt das freie Gerät nach.
    /// Die Tastatur füllt (je nach Profil) einen leeren Platz.
    /// </summary>
    public static Dictionary<int, ControllerDescriptor> AssignDevices(ControllerProfile profile,
        IReadOnlyDictionary<int, ControllerDescriptor> slots, IReadOnlyList<ControllerDescriptor> connected)
    {
        var result = new Dictionary<int, ControllerDescriptor>();
        var used = new HashSet<string>();

        // 1. Feste Zuordnungen (nur verbundene Geräte bzw. die Tastatur)
        for (int p = 1; p <= 4; p++)
        {
            var key = profile.For(p).DeviceKey;
            if (key == null)
                continue;
            var device = connected.FirstOrDefault(d => d.Key == key)
                         ?? (key == ControllerDescriptor.Keyboard.Key ? ControllerDescriptor.Keyboard : null);
            if (device != null && used.Add(device.Key))
                result[p] = device;
        }
        // 2. Spieler-Platz: das Gerät auf Platz p, sofern nicht schon vergeben
        for (int p = 1; p <= 4; p++)
        {
            if (!result.ContainsKey(p) && slots.TryGetValue(p, out var device) && used.Add(device.Key))
                result[p] = device;
        }
        // 3. Übrige Plätze mit noch freien Geräten auffüllen (in Platz-Reihenfolge)
        var spare = new Queue<ControllerDescriptor>(slots.OrderBy(kv => kv.Key).Select(kv => kv.Value).Where(d => !used.Contains(d.Key)));
        for (int p = 1; p <= 4 && spare.Count > 0; p++)
        {
            if (result.ContainsKey(p))
                continue;
            var device = spare.Dequeue();
            used.Add(device.Key);
            result[p] = device;
        }
        // 4. Tastatur
        var keyboardPlayer = profile.KeyboardPlayer == -1 ? (result.ContainsKey(1) ? 0 : 1) : profile.KeyboardPlayer;
        if (keyboardPlayer is >= 1 and <= 4 && !result.ContainsKey(keyboardPlayer) && !used.Contains(ControllerDescriptor.Keyboard.Key))
            result[keyboardPlayer] = ControllerDescriptor.Keyboard;
        return result;
    }

    /// <summary>
    /// Welcher Spieler eine echte Wii Remote in Wii-Spielen wird. Führt SDL die Wii Remotes als Controller, gilt deren
    /// Spieler-Platz (erkennbar an der LED). Sonst belegt Dolphin die freien Plätze des Standardprofils der Reihe nach
    /// (DolphinBar-Slot 1–4, dann Bluetooth) – genau wie <see cref="DolphinInputWriter"/> sie als „echte Wii Remote“ einträgt.
    /// 0 = wird nicht verwendet.
    /// </summary>
    public IReadOnlyDictionary<string, int> WiiRemotePlayers(IReadOnlyList<WiiRemoteInfo> remotes)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sdlWiimotes = Controllers.Devices.Where(d => d.Kind == ControllerKind.WiiRemote).ToList();
        if (sdlWiimotes.Count > 0)
        {
            foreach (var r in remotes)
                result[r.Path] = r.Status is { } s ? WiiRemoteProtocol.PlayerFromLeds(s.Leds) : 0;
            return result;
        }
        var free = FreeWiiSlots();
        var k = 0;
        foreach (var r in remotes.Where(r => r.Responding).OrderBy(r => r.ViaDolphinBar ? 0 : 1).ThenBy(r => r.Slot))
            result[r.Path] = k < free.Count ? free[k++] : 0;
        return result;
    }

    /// <summary>Spielerplätze, die in Wii-Spielen (Standardprofil) für echte Wii Remotes frei bleiben.</summary>
    public IReadOnlyList<int> FreeWiiSlots()
    {
        if (!Profiles.RealWiimotesInFreeSlots)
            return [];
        var profile = Profiles.Default;
        var slots = Controllers.Devices.Where(d => d.Player > 0 && d.Kind != ControllerKind.WiiRemote)
            .ToDictionary(d => d.Player, Controllers.Describe);
        var connected = Controllers.Devices.Where(d => d.Kind != ControllerKind.WiiRemote).Select(Controllers.Describe).ToList();
        var used = AssignDevices(profile, slots, connected);
        return Enumerable.Range(1, 4).Where(p => !used.ContainsKey(p)).ToList();
    }

    /// <summary>Wii-Remote-Einstellungen aus Dolphin (SYSCONF + Dolphin.ini).</summary>
    public WiiRemoteSettings ReadWiiSettings()
    {
        var user = _hub.Adapters.Dolphin.UserDirectory();
        var sys = WiiSysconf.Load(WiiSysconf.PathIn(user));
        var ini = new IniFile(Path.Combine(user, "Config", "Dolphin.ini"));
        return new WiiRemoteSettings(
            sys != null,
            sys?.Get(WiiSysconf.SensorBarPosition) ?? 1,
            sys?.Get(WiiSysconf.SensorBarSensitivity) ?? 3,
            sys?.Get(WiiSysconf.SpeakerVolume) ?? 88,
            (sys?.Get(WiiSysconf.WiimoteMotor) ?? 1) != 0,
            string.Equals(ini.Get("Core", "WiimoteEnableSpeaker"), "True", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Speichert die Einstellungen; Fehlermeldung oder null.</summary>
    public string? WriteWiiSettings(WiiRemoteSettings s)
    {
        if (System.Diagnostics.Process.GetProcessesByName("Dolphin").Length > 0)
            return "Dolphin läuft gerade – bitte zuerst beenden, sonst überschreibt Dolphin die Einstellungen.";
        var user = _hub.Adapters.Dolphin.UserDirectory();
        var sys = WiiSysconf.Load(WiiSysconf.PathIn(user));
        if (sys == null)
            return "Wii-Systemeinstellungen (SYSCONF) fehlen – einmal ein Wii-Spiel in Dolphin starten, dann erneut versuchen.";
        sys.Set(WiiSysconf.SensorBarPosition, s.SensorBarTop ? 1 : 0);
        sys.Set(WiiSysconf.SensorBarSensitivity, Math.Clamp(s.Sensitivity, 1, 5));
        sys.Set(WiiSysconf.SpeakerVolume, Math.Clamp(s.SpeakerVolume, 0, 127));
        sys.Set(WiiSysconf.WiimoteMotor, s.Rumble ? 1 : 0);
        sys.Save(_hub.Backups);
        var ini = new IniFile(Path.Combine(user, "Config", "Dolphin.ini"));
        ini.Set("Core", "WiimoteEnableSpeaker", s.RealSpeaker ? "True" : "False");
        ini.Set("Core", "WiimoteContinuousScanning", "True");
        ini.Save();
        HubLog.Info($"Wii-Remote-Einstellungen gespeichert: Leiste {(s.SensorBarTop ? "oben" : "unten")}, Empfindlichkeit {s.Sensitivity}, " +
                    $"Lautsprecher {s.SpeakerVolume}/{(s.RealSpeaker ? "an" : "aus")}, Vibration {(s.Rumble ? "an" : "aus")}");
        return null;
    }

    /// <summary>Schritt „Controllerprofil laden“ der Start-Pipeline.</summary>
    public async Task PrepareAsync(GameEntry game, IEmulatorAdapter adapter, IProgress<string>? progress = null)
    {
        // Wii Remotes freigeben, damit Dolphin sie exklusiv bekommt
        Wiimotes.Stop();
        // Frisch einlesen: Emulatoren zählen gleiche Controller in der Reihenfolge einer frischen Erkennung
        // (Dolphin „SDL/0/…“, „SDL/1/…“) – nach einem Neu-Verbinden wäre die Hub-Reihenfolge sonst eine andere.
        Controllers.Reenumerate();
        var setup = Resolve(game, adapter.Id);
        var profile = Profiles.For(game.Id);
        HubLog.Info("Controller: " + string.Join(", ", setup.Players.Select(p => $"P{p.Player}={p.Device.Name} ({p.Mode}, {p.Pad})")));

        if (setup.NeedsCompatibilityLayer)
            setup = await ActivateCompatibilityAsync(setup, game, profile, progress);

        try
        {
            WriteConfig(setup, game, adapter, progress);
        }
        finally
        {
            // Geräte freigeben, damit der Emulator (bzw. Dolphins Wii-Remote-Durchreichung) sie exklusiv bekommt
            Controllers.ReleaseUntilGameEnds();
        }
    }

    private void WriteConfig(InputSetup setup, GameEntry game, IEmulatorAdapter adapter, IProgress<string>? progress)
    {
        if (!setup.ManageMappings || setup.Players.Count == 0)
            return;
        IInputConfigWriter? writer = adapter.Id switch
        {
            EmulatorIds.Dolphin => new DolphinInputWriter(_hub.Adapters.Dolphin.UserDirectory(), _hub.Backups),
            EmulatorIds.Cemu => new CemuInputWriter(_hub.Adapters.Cemu.DataDirectory(), _hub.Backups),
            EmulatorIds.Switch => _hub.Adapters.Switch is EdenAdapter eden ? new EdenInputWriter(eden) : null,
            EmulatorIds.MelonDS => _hub.Adapters.MelonDS.ConfigFile() is { } melon ? new MelonDsInputWriter(melon, _hub.Backups) : null,
            EmulatorIds.Azahar => _hub.Adapters.Azahar.ConfigFile() is { } azahar ? new AzaharInputWriter(azahar, _hub.Backups) : null,
            _ => null, // WiiCompiled verwaltet seine Belegung selbst (F10 im Spiel)
        };
        if (writer == null)
            return;
        try
        {
            var text = writer.Apply(setup, game);
            HubLog.Info(text);
            progress?.Report(text);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Controller-Konfiguration konnte nicht geschrieben werden", ex);
        }
    }

    private async Task<InputSetup> ActivateCompatibilityAsync(InputSetup setup, GameEntry game, ControllerProfile profile,
        IProgress<string>? progress)
    {
        progress?.Report("Kompatibilitätsschicht (PadForge) wird gestartet …");
        var before = Controllers.Devices.Select(d => d.Key).ToHashSet();
        if (!await PadForge.EnsureRunningAsync())
        {
            HubLog.Warn("PadForge nicht erreichbar – Controller werden direkt übergeben");
            return WithMode(setup, PlayerMode.Native);
        }
        var name = string.IsNullOrWhiteSpace(profile.PadForgeProfile) ? DefaultPadForgeProfile : profile.PadForgeProfile;
        var response = await PadForge.ActivateAsync(name);
        _padForgeActive = true;
        if (response?.StartsWith("error unknown-profile") == true)
            HubLog.Info($"PadForge-Profil „{name}“ existiert nicht – PadForge nutzt seine aktuelle Zuordnung");

        // Virtuelle Xbox-Controller von PadForge abwarten (Hotplug)
        var virtualPads = new List<ControllerDescriptor>();
        var needed = setup.Players.Count(p => p.Mode == PlayerMode.Compatibility);
        Controllers.SuppressAutoAssign = true;
        try
        {
            for (int i = 0; i < 30 && virtualPads.Count < needed; i++)
            {
                Controllers.Poll(0.1, deliverActions: false, gameRunning: false);
                virtualPads = Controllers.Devices
                    .Where(d => !before.Contains(d.Key) && d.Kind == ControllerKind.Xbox)
                    .Select(Controllers.Describe).ToList();
                await Task.Delay(100);
            }
        }
        finally
        {
            Controllers.SuppressAutoAssign = false;
        }

        var physicalXbox = Controllers.Devices.Count(d => before.Contains(d.Key) && d.Kind == ControllerKind.Xbox);
        var result = new InputSetup { ManageMappings = setup.ManageMappings, Rumble = setup.Rumble, RealWiimotesInFreeSlots = setup.RealWiimotesInFreeSlots, Remap = setup.Remap };
        var k = 0;
        foreach (var p in setup.Players)
        {
            if (p.Mode != PlayerMode.Compatibility)
            {
                result.Players.Add(p);
                continue;
            }
            var pad = k < virtualPads.Count
                ? virtualPads[k]
                : new ControllerDescriptor
                {
                    Key = $"padforge:{k}",
                    Name = "PadForge (virtueller Xbox-Controller)",
                    Kind = ControllerKind.Xbox,
                    XInputIndex = Math.Min(3, physicalXbox + k),
                    IsVirtual = true,
                };
            k++;
            var emulated = p.Pad == EmulatedPad.Wiimote && p.Device.Kind == ControllerKind.WiiRemote ? EmulatedPad.Wiimote : p.Pad;
            result.Players.Add(p with { Device = pad with { Kind = ControllerKind.Xbox }, Pad = emulated });
        }
        return result;
    }

    private static InputSetup WithMode(InputSetup setup, PlayerMode mode)
    {
        var copy = new InputSetup { ManageMappings = setup.ManageMappings, Rumble = setup.Rumble, RealWiimotesInFreeSlots = setup.RealWiimotesInFreeSlots, Remap = setup.Remap };
        copy.Players.AddRange(setup.Players.Select(p => p with { Mode = mode }));
        return copy;
    }

    /// <summary>Nach dem Spiel: PadForge-Profil wieder freigeben.</summary>
    public async Task FinishAsync()
    {
        Controllers.EndHold();
        if (_padForgeActive)
        {
            _padForgeActive = false;
            await PadForge.DeactivateAsync();
        }
    }
}

/// <summary>Wii-Remote-/Sensorleisten-Einstellungen, wie Dolphin sie an Wii-Spiele weitergibt.</summary>
public sealed record WiiRemoteSettings(bool Available, int SensorBarPosition, int Sensitivity, int SpeakerVolume, bool Rumble, bool RealSpeaker)
{
    public bool SensorBarTop => SensorBarPosition != 0;
}
