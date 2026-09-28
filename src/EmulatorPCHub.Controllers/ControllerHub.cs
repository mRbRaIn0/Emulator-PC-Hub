using EmulatorPCHub.Controllers.Sdl;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Controllers;

/// <summary>Ein erkannter physischer Controller.</summary>
public sealed class ControllerDevice
{
    internal IntPtr Handle;
    internal Sdl3.Button AcceptButton = Sdl3.Button.South;
    internal Sdl3.Button BackButton = Sdl3.Button.East;
    internal Sdl3.Button XButton = Sdl3.Button.West;
    internal Sdl3.Button YButton = Sdl3.Button.North;

    public uint InstanceId { get; internal set; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public ControllerKind Kind { get; init; }
    public ushort Vendor { get; init; }
    public ushort Product { get; init; }
    public string Guid { get; init; } = "";
    /// <summary>SDL-Mapping (Rohknöpfe/-achsen), z. B. für die Eden-Konfiguration.</summary>
    public string? SdlMapping { get; init; }
    public string? Serial { get; init; }
    public bool NintendoLayout { get; internal set; }
    public int? XInputIndex { get; internal set; }
    public bool IsVirtual { get; init; }
    public bool Wired { get; internal set; }
    public int? BatteryPercent { get; internal set; }
    public bool Charging { get; internal set; }
    /// <summary>Spieler 1–4, 0 = nicht zugewiesen.</summary>
    public int Player { get; internal set; }
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.Now;

    public string BatteryText => Wired && BatteryPercent == null ? "Kabel"
        : BatteryPercent is { } p ? $"{p} %{(Charging ? " ⚡" : "")}"
        : "–";
}

/// <summary>
/// Controller-Verwaltung des Hubs auf Basis von SDL3: automatische Erkennung inkl. Hotplug,
/// Spieler 1–4 (merkt sich die letzte Zuordnung), Akkustand, Vibration/Player-LEDs und Menü-Navigation.
/// Xbox, DualSense/DualShock, Switch Pro/Joy-Con, Wii Remote und Wii U Pro werden erkannt.
/// Fällt auf XInput zurück, falls SDL3 nicht geladen werden kann. Während ein Spiel läuft, gibt der Hub
/// die Geräte frei (nur XInput wird für Home + Minus weiter abgefragt).
/// </summary>
public sealed class ControllerHub
{
    private const short StickThreshold = 16000;
    private readonly ControllerService _xinput = new();
    private readonly List<ControllerDevice> _devices = [];
    private readonly Dictionary<NavAction, double> _repeatAt = [];
    private readonly HashSet<NavAction> _held = [];
    private double _time;
    private double _batteryTimer;
    private double _homeComboHeld;
    private bool _released;
    private bool _holdUntilGame;
    private int _joinNext;

    public bool SdlAvailable { get; private set; }
    public string? SdlError { get; private set; }

    /// <summary>Gespeicherte Zuordnung laden: Geräteschlüssel → Spieler.</summary>
    public Func<string, int?>? LoadAssignment { get; set; }
    /// <summary>Zuordnung speichern.</summary>
    public Action<string, int>? SaveAssignment { get; set; }

    public bool SwapConfirm
    {
        get => _xinput.SwapConfirm;
        set => _xinput.SwapConfirm = value;
    }

    public bool JoinActive => _joinNext > 0;

    /// <summary>Neue Geräte (z. B. virtuelle PadForge-Controller) keinem Spieler zuweisen.</summary>
    public bool SuppressAutoAssign { get; set; }

    public event Action<NavAction>? Action;
    public event Action<ControllerDevice, bool>? DeviceChanged;
    public event Action? PlayersChanged;
    public event Action? HomeComboHeld;
    public event Action<ControllerDevice>? LowBattery;

    public IReadOnlyList<ControllerDevice> Devices => _devices.OrderBy(d => d.Player == 0 ? 99 : d.Player).ThenBy(d => d.ConnectedAt).ToList();

    public int ConnectedCount => SdlAvailable ? _devices.Count : _xinput.ConnectedCount;

    public ControllerDevice? ForPlayer(int player) => _devices.FirstOrDefault(d => d.Player == player);

    public ControllerHub()
    {
        _xinput.HomeComboHeld += () => HomeComboHeld?.Invoke();
        _xinput.Action += a => { if (!SdlAvailable) Action?.Invoke(a); };
    }

    /// <summary>SDL3 initialisieren (auf dem UI-Thread aufrufen).</summary>
    public void Start()
    {
        try
        {
            ApplyHints();
            if (!Sdl3.Init(Sdl3.InitGamepad))
            {
                SdlError = Sdl3.GetError();
                HubLog.Warn($"SDL3 konnte nicht initialisiert werden: {SdlError} – XInput-Fallback aktiv");
                return;
            }
            SdlAvailable = true;
            HubLog.Info("SDL3-Controller-Verwaltung aktiv");
            OpenAll();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            SdlError = ex.Message;
            HubLog.Warn("SDL3.dll nicht verfügbar – XInput-Fallback aktiv", ex);
        }
    }

    private static void ApplyHints()
    {
        Sdl3.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_WII", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_PS5", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_PS4", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_SWITCH", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_JOY_CONS", "1");
        Sdl3.SetHint("SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS", "1");
    }

    /// <summary>
    /// Controller frisch einlesen (SDL neu starten). Danach entspricht die Reihenfolge gleicher Geräte der eines frisch
    /// gestarteten Emulators – wichtig für Dolphins „SDL/0/…“, „SDL/1/…“ und die Ports in Cemu/Eden, wenn zwei gleiche
    /// Controller verbunden sind und einer zwischendurch neu verbunden wurde. Spieler-Zuordnungen bleiben erhalten.
    /// </summary>
    public unsafe void Reenumerate()
    {
        if (!SdlAvailable || _released)
            return;
        var previous = _devices.ToList();
        foreach (var d in previous.Where(d => d.Handle != IntPtr.Zero))
            Sdl3.CloseGamepad(d.Handle);
        _devices.Clear();
        Sdl3.Quit();
        ApplyHints();
        if (!Sdl3.Init(Sdl3.InitGamepad))
        {
            SdlAvailable = false;
            SdlError = Sdl3.GetError();
            HubLog.Warn($"SDL3 konnte nicht neu gestartet werden: {SdlError}");
            return;
        }
        _silent = true;
        try
        {
            OpenAll();
        }
        finally
        {
            _silent = false;
        }
        foreach (var gone in previous.Where(p => _devices.All(d => d.Key != p.Key)))
            DeviceChanged?.Invoke(gone, false);
        foreach (var d in _devices)
        {
            var old = previous.FirstOrDefault(p => p.Key == d.Key);
            if (old != null && old.Player != d.Player && (old.Player == 0 || ForPlayer(old.Player) == null))
            {
                d.Player = old.Player;
                ApplyLed(d);
            }
        }
        PlayersChanged?.Invoke();
    }

    private unsafe void OpenAll()
    {
        int count;
        var ids = Sdl3.GetGamepads(&count);
        if (ids == null)
            return;
        try
        {
            for (int i = 0; i < count; i++)
                Open(ids[i]);
        }
        finally
        {
            Sdl3.Free(ids);
        }
    }

    private unsafe void Open(uint id)
    {
        if (_devices.Any(d => d.InstanceId == id))
            return;
        var handle = Sdl3.OpenGamepad(id);
        if (handle == IntPtr.Zero)
            return;
        var name = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Sdl3.GetGamepadNamePtr(handle)) ?? "Controller";
        var serial = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Sdl3.GetGamepadSerialPtr(handle));
        var type = Sdl3.GetGamepadType(handle);
        var vendor = Sdl3.GetGamepadVendor(handle);
        var product = Sdl3.GetGamepadProduct(handle);
        var guid = Sdl3.GuidString(id);
        var kind = Classify(name, type, vendor);
        var sameGuid = _devices.Count(d => d.Guid == guid);
        var key = !string.IsNullOrEmpty(serial) ? $"{guid}:{serial}" : $"{guid}:{sameGuid}";
        var device = new ControllerDevice
        {
            Handle = handle,
            InstanceId = id,
            Key = key,
            Name = name,
            Kind = kind,
            Vendor = vendor,
            Product = product,
            Guid = guid,
            SdlMapping = Sdl3.GetGamepadMapping(handle),
            Serial = serial,
            IsVirtual = Sdl3.IsJoystickVirtual(id),
        };

        // Beschriftungen auswerten: welcher Knopf ist „A“ (bzw. Kreuz)?
        foreach (var b in new[] { Sdl3.Button.South, Sdl3.Button.East, Sdl3.Button.West, Sdl3.Button.North })
        {
            switch (Sdl3.GetGamepadButtonLabel(handle, b))
            {
                case Sdl3.ButtonLabel.A or Sdl3.ButtonLabel.Cross: device.AcceptButton = b; break;
                case Sdl3.ButtonLabel.B or Sdl3.ButtonLabel.Circle: device.BackButton = b; break;
                case Sdl3.ButtonLabel.X or Sdl3.ButtonLabel.Square: device.XButton = b; break;
                case Sdl3.ButtonLabel.Y or Sdl3.ButtonLabel.Triangle: device.YButton = b; break;
            }
        }
        device.NintendoLayout = device.AcceptButton == Sdl3.Button.East;
        if (kind == ControllerKind.Xbox)
        {
            var idx = Sdl3.GetGamepadPlayerIndex(handle);
            device.XInputIndex = idx is >= 0 and < 4 ? idx : null;
        }
        _devices.Add(device);
        UpdatePower(device);
        AssignPlayer(device);
        HubLog.Info($"Controller verbunden: {name} ({kind}, {guid}) → Spieler {device.Player}");
        if (!_silent)
            DeviceChanged?.Invoke(device, true);
    }

    private void Close(uint id)
    {
        var device = _devices.FirstOrDefault(d => d.InstanceId == id);
        if (device == null)
            return;
        Sdl3.CloseGamepad(device.Handle);
        _devices.Remove(device);
        HubLog.Info($"Controller getrennt: {device.Name}");
        DeviceChanged?.Invoke(device, false);
        PlayersChanged?.Invoke();
    }

    internal static ControllerKind Classify(string name, Sdl3.GamepadType type, ushort vendor)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("wii u pro"))
            return ControllerKind.WiiUPro;
        if (n.Contains("wii remote") || n.Contains("wiimote"))
            return ControllerKind.WiiRemote;
        return type switch
        {
            Sdl3.GamepadType.Xbox360 or Sdl3.GamepadType.XboxOne => ControllerKind.Xbox,
            Sdl3.GamepadType.PS5 => ControllerKind.PlayStation5,
            Sdl3.GamepadType.PS4 or Sdl3.GamepadType.PS3 => ControllerKind.PlayStation4,
            Sdl3.GamepadType.SwitchPro => ControllerKind.SwitchPro,
            Sdl3.GamepadType.JoyConLeft or Sdl3.GamepadType.JoyConRight or Sdl3.GamepadType.JoyConPair => ControllerKind.JoyCon,
            _ when vendor == 0x045E => ControllerKind.Xbox,
            _ when vendor == 0x054C => ControllerKind.PlayStation5,
            _ when vendor == 0x057E => ControllerKind.SwitchPro,
            _ => ControllerKind.Generic,
        };
    }

    // ------------------------------------------------------------------
    // Spieler-Zuordnung
    // ------------------------------------------------------------------

    private void AssignPlayer(ControllerDevice device)
    {
        if (_joinNext > 0 || SuppressAutoAssign)
        {
            device.Player = 0;
            return;
        }
        var saved = LoadAssignment?.Invoke(device.Key);
        if (saved is >= 1 and <= 4 && ForPlayer(saved.Value) == null)
            device.Player = saved.Value;
        else
            device.Player = Enumerable.Range(1, 4).FirstOrDefault(p => ForPlayer(p) == null);
        ApplyLed(device);
        PlayersChanged?.Invoke();
    }

    /// <summary>Setzt einen Controller auf einen Spieler-Platz; ein dort vorhandener Controller tauscht den Platz.</summary>
    public void SetPlayer(ControllerDevice device, int player)
    {
        player = Math.Clamp(player, 0, 4);
        var other = player > 0 ? ForPlayer(player) : null;
        if (other != null && other != device)
        {
            other.Player = device.Player;
            ApplyLed(other);
            SaveAssignment?.Invoke(other.Key, other.Player);
        }
        device.Player = player;
        ApplyLed(device);
        SaveAssignment?.Invoke(device.Key, player);
        PlayersChanged?.Invoke();
    }

    /// <summary>Automatisch nach Verbindungsreihenfolge verteilen.</summary>
    public void AutoAssign()
    {
        var p = 1;
        foreach (var d in _devices.OrderBy(d => d.ConnectedAt))
        {
            d.Player = p <= 4 ? p++ : 0;
            ApplyLed(d);
            SaveAssignment?.Invoke(d.Key, d.Player);
        }
        PlayersChanged?.Invoke();
    }

    /// <summary>„Beitreten“: Controller werden in der Reihenfolge, in der A gedrückt wird, Spieler 1, 2, 3, 4.</summary>
    public void BeginJoin()
    {
        foreach (var d in _devices)
            d.Player = 0;
        _joinNext = 1;
        PlayersChanged?.Invoke();
    }

    public void EndJoin()
    {
        _joinNext = 0;
        foreach (var d in _devices.Where(d => d.Player == 0))
            AssignPlayer(d);
        foreach (var d in _devices)
            SaveAssignment?.Invoke(d.Key, d.Player);
        PlayersChanged?.Invoke();
    }

    private static void ApplyLed(ControllerDevice d)
    {
        if (d.Handle != IntPtr.Zero && d.Kind != ControllerKind.Xbox)
            Sdl3.SetGamepadPlayerIndex(d.Handle, d.Player - 1);
    }

    // ------------------------------------------------------------------
    // Polling
    // ------------------------------------------------------------------

    public unsafe void Poll(double dt, bool deliverActions, bool gameRunning)
    {
        _time += dt;
        if (!SdlAvailable)
        {
            _xinput.Poll(dt, deliverActions);
            return;
        }

        if (gameRunning)
        {
            _holdUntilGame = false;
            if (!_released)
                ReleaseForGame();
        }
        else if (_released && !_holdUntilGame)
        {
            Reacquire();
        }
        if (_released)
        {
            // Xbox: Home + Minus über XInput. PlayStation: nach kurzer Wartezeit lesend wieder öffnen
            // (PS + Create), nachdem der Emulator seine Geräte übernommen hat. Nintendo-Controller bleiben
            // unangetastet, weil ein zweiter HID-Zugriff ihre Verbindung zum Emulator stören kann.
            _xinput.Poll(dt, deliverActions: false);
            if (WatchComboDuringGame && gameRunning)
                PollComboDuringGame(dt);
            return;
        }

        Sdl3.Event ev;
        while (Sdl3.PollEvent(&ev))
        {
            switch (ev.Type)
            {
                case Sdl3.EventGamepadAdded:
                    Open(ev.Which);
                    break;
                case Sdl3.EventGamepadRemoved:
                    Close(ev.Which);
                    break;
            }
        }

        _batteryTimer -= dt;
        if (_batteryTimer <= 0)
        {
            _batteryTimer = 5;
            foreach (var d in _devices)
                UpdatePower(d);
        }

        var current = new HashSet<NavAction>();
        var combo = false;
        foreach (var d in _devices)
        {
            bool Down(Sdl3.Button b) => Sdl3.GetGamepadButton(d.Handle, b);
            var lx = Sdl3.GetGamepadAxis(d.Handle, Sdl3.Axis.LeftX);
            var ly = Sdl3.GetGamepadAxis(d.Handle, Sdl3.Axis.LeftY);
            var accept = Down(SwapConfirm ? d.BackButton : d.AcceptButton);
            var back = Down(SwapConfirm ? d.AcceptButton : d.BackButton);

            if (_joinNext > 0 && d.Player == 0 && accept && _joinNext <= 4)
            {
                d.Player = _joinNext++;
                ApplyLed(d);
                SaveAssignment?.Invoke(d.Key, d.Player);
                Rumble(d, 0.4f, 150);
                PlayersChanged?.Invoke();
                if (_joinNext > 4 || _devices.All(x => x.Player > 0))
                    _joinNext = 0;
                continue;
            }

            if (Down(Sdl3.Button.DpadUp) || ly < -StickThreshold) current.Add(NavAction.Up);
            if (Down(Sdl3.Button.DpadDown) || ly > StickThreshold) current.Add(NavAction.Down);
            if (Down(Sdl3.Button.DpadLeft) || lx < -StickThreshold) current.Add(NavAction.Left);
            if (Down(Sdl3.Button.DpadRight) || lx > StickThreshold) current.Add(NavAction.Right);
            if (accept) current.Add(NavAction.Accept);
            if (back) current.Add(NavAction.Back);
            if (Down(d.XButton)) current.Add(NavAction.X);
            if (Down(d.YButton)) current.Add(NavAction.Y);
            if (Down(Sdl3.Button.Start)) current.Add(NavAction.Plus);
            if (Down(Sdl3.Button.Back)) current.Add(NavAction.Minus);
            if (Down(Sdl3.Button.Guide)) current.Add(NavAction.Home);
            if (Down(Sdl3.Button.LeftShoulder)) current.Add(NavAction.L);
            if (Down(Sdl3.Button.RightShoulder)) current.Add(NavAction.R);
            if (Down(Sdl3.Button.Guide) && Down(Sdl3.Button.Back))
                combo = true;
        }

        if (combo)
        {
            _homeComboHeld += dt;
            if (_homeComboHeld >= 1.5)
            {
                _homeComboHeld = double.NegativeInfinity;
                HomeComboHeld?.Invoke();
            }
        }
        else
        {
            _homeComboHeld = 0;
        }

        foreach (var action in current)
        {
            var repeatable = action is NavAction.Up or NavAction.Down or NavAction.Left or NavAction.Right;
            if (!_held.Contains(action))
            {
                _held.Add(action);
                _repeatAt[action] = _time + ControllerService.RepeatDelay;
                if (deliverActions)
                    Action?.Invoke(action);
            }
            else if (repeatable && _time >= _repeatAt[action])
            {
                _repeatAt[action] = _time + ControllerService.RepeatInterval;
                if (deliverActions)
                    Action?.Invoke(action);
            }
        }
        _held.RemoveWhere(a => !current.Contains(a));
    }

    private unsafe void UpdatePower(ControllerDevice d)
    {
        int percent = -1;
        var state = Sdl3.GetGamepadPowerInfo(d.Handle, &percent);
        var before = d.BatteryPercent;
        d.Charging = state is Sdl3.PowerState.Charging;
        d.Wired = Sdl3.GetGamepadConnectionState(d.Handle) == Sdl3.ConnectionState.Wired;
        d.BatteryPercent = state is Sdl3.PowerState.OnBattery or Sdl3.PowerState.Charging or Sdl3.PowerState.Charged && percent >= 0
            ? percent
            : state == Sdl3.PowerState.Charged ? 100 : null;
        if (d.BatteryPercent is <= 15 && (before is null or > 15) && !d.Charging)
            LowBattery?.Invoke(d);
    }

    /// <summary>Vor dem Emulator-Start freigeben und freigegeben lassen, bis das Spiel läuft und wieder endet.</summary>
    public void ReleaseUntilGameEnds()
    {
        _holdUntilGame = true;
        ReleaseForGame();
    }

    /// <summary>Freigabe aufheben, falls der Start abgebrochen wurde.</summary>
    public void EndHold() => _holdUntilGame = false;

    /// <summary>Home + Minus (PS + Create) auch mit PlayStation-Controllern während des Spiels erkennen.</summary>
    public bool WatchComboDuringGame { get; set; } = true;

    private double? _comboWatchSince;
    private bool _silent;

    private unsafe void PollComboDuringGame(double dt)
    {
        _comboWatchSince ??= _time;
        if (_time - _comboWatchSince < 5)
            return; // Emulator zuerst starten lassen

        foreach (var d in _devices.Where(d => d.Handle == IntPtr.Zero && d.Kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4))
            d.Handle = Sdl3.OpenGamepad(d.InstanceId);

        Sdl3.Event ev;
        while (Sdl3.PollEvent(&ev))
        {
            var which = ev.Which;
            if (ev.Type == Sdl3.EventGamepadRemoved && _devices.FirstOrDefault(x => x.InstanceId == which) is { } gone)
            {
                if (gone.Handle != IntPtr.Zero)
                    Sdl3.CloseGamepad(gone.Handle);
                gone.Handle = IntPtr.Zero;
            }
        }

        var combo = _devices.Any(d => d.Handle != IntPtr.Zero
                                      && Sdl3.GetGamepadButton(d.Handle, Sdl3.Button.Guide)
                                      && Sdl3.GetGamepadButton(d.Handle, Sdl3.Button.Back));
        if (combo)
        {
            _homeComboHeld += dt;
            if (_homeComboHeld >= 1.5)
            {
                _homeComboHeld = double.NegativeInfinity;
                HomeComboHeld?.Invoke();
            }
        }
        else
        {
            _homeComboHeld = 0;
        }
    }

    /// <summary>Vor dem Spielstart: Geräte schließen, damit Emulator bzw. PadForge sie exklusiv nutzen können.</summary>
    public void ReleaseForGame()
    {
        if (!SdlAvailable || _released)
            return;
        foreach (var d in _devices)
        {
            Sdl3.CloseGamepad(d.Handle);
            d.Handle = IntPtr.Zero;
        }
        _released = true;
    }

    /// <summary>Nach dem Spiel: Geräte wieder öffnen (Zuordnungen bleiben erhalten).</summary>
    public unsafe void Reacquire()
    {
        if (!SdlAvailable || !_released)
            return;
        _released = false;
        _comboWatchSince = null;
        var previous = _devices.ToList();
        foreach (var d in previous.Where(d => d.Handle != IntPtr.Zero))
            Sdl3.CloseGamepad(d.Handle);
        _devices.Clear();
        // Ereignisse aus der Spielzeit verwerfen
        Sdl3.Event ev;
        while (Sdl3.PollEvent(&ev)) { }
        _silent = true;
        try
        {
            OpenAll();
        }
        finally
        {
            _silent = false;
        }
        // Neu hinzugekommene Geräte melden, bereits bekannte nicht erneut
        foreach (var d in _devices.Where(d => previous.All(p => p.Key != d.Key)))
            DeviceChanged?.Invoke(d, true);
        foreach (var gone in previous.Where(p => _devices.All(d => d.Key != p.Key)))
            DeviceChanged?.Invoke(gone, false);
        foreach (var d in _devices)
        {
            var old = previous.FirstOrDefault(p => p.Key == d.Key);
            if (old != null && old.Player != d.Player && (old.Player == 0 || ForPlayer(old.Player) == null))
            {
                d.Player = old.Player;
                ApplyLed(d);
            }
        }
        PlayersChanged?.Invoke();
    }

    // ------------------------------------------------------------------
    // Vibration
    // ------------------------------------------------------------------

    public void Rumble(ControllerDevice d, float strength, int ms)
    {
        if (d.Handle != IntPtr.Zero)
        {
            var v = (ushort)(Math.Clamp(strength, 0, 1) * 65535);
            Sdl3.RumbleGamepad(d.Handle, v, v, (uint)ms);
        }
    }

    public void Identify(ControllerDevice d) => Rumble(d, 0.7f, 400);

    public void RumbleAll(float strength, int ms)
    {
        if (!SdlAvailable)
        {
            _xinput.RumbleAll(strength, ms);
            return;
        }
        foreach (var d in _devices)
            Rumble(d, strength, ms);
    }

    // ------------------------------------------------------------------
    // Beschreibung für Emulatoren
    // ------------------------------------------------------------------

    public ControllerDescriptor Describe(ControllerDevice d)
    {
        var ordered = _devices.OrderBy(x => x.InstanceId).ToList();
        var idx = ordered.IndexOf(d);
        return new ControllerDescriptor
        {
            Key = d.Key,
            Name = d.Name,
            Kind = d.Kind,
            SdlGuid = d.Guid,
            SdlMapping = d.SdlMapping,
            SdlIndex = idx,
            GuidIndex = ordered.Take(idx).Count(x => x.Guid == d.Guid),
            NameIndex = ordered.Take(idx).Count(x => x.Name == d.Name),
            XInputIndex = d.XInputIndex,
            NintendoLayout = d.NintendoLayout,
            IsVirtual = d.IsVirtual,
        };
    }

    /// <summary>Beschreibungen aller Geräte (für XInput-Fallback ohne SDL: Xbox-Slots).</summary>
    public IReadOnlyList<ControllerDescriptor> DescribeAll()
    {
        if (SdlAvailable)
            return _devices.Select(Describe).ToList();
        return _xinput.Controllers().Select(c => new ControllerDescriptor
        {
            Key = $"xinput:{c.Slot}",
            Name = c.Name,
            Kind = ControllerKind.Xbox,
            XInputIndex = c.Slot,
        }).ToList();
    }

    public IReadOnlyList<ControllerInfo> LegacyInfo() => _xinput.Controllers();
}
