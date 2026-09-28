using System.Runtime.InteropServices;

namespace EmulatorPCHub.Controllers;

/// <summary>Abstrakte Navigationsaktionen – unabhängig vom Controller-Typ (Plan Abschnitt 4).</summary>
public enum NavAction
{
    Up,
    Down,
    Left,
    Right,
    Accept,   // A
    Back,     // B
    X,
    Y,
    Plus,     // Start/+ = Optionen
    Minus,    // Back/View/−
    Home,     // Guide/Home = Hub-Menü
    L,
    R,
}

[Flags]
public enum PadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    Guide = 0x0400,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

public sealed record ControllerInfo(int Slot, string Name, bool Connected, int? BatteryPercent, bool Wired);

/// <summary>
/// Pollt XInput-Controller (Xbox, sowie Switch Pro/Joy-Con/DualSense/DualShock über XInput-Mapping wie Steam Input,
/// DS4Windows oder BetterJoy) und erzeugt Navigationsaktionen mit Tastenwiederholung.
/// Der Guide/Home-Button wird über die erweiterte XInput-Funktion gelesen.
/// </summary>
public sealed class ControllerService
{
    private const short StickThreshold = 16000;
    private readonly PadButtons[] _prev = new PadButtons[4];
    private readonly bool[] _connected = new bool[4];
    private readonly Dictionary<NavAction, double> _repeatAt = [];
    private readonly HashSet<NavAction> _held = [];
    private double _time;
    private double _homeComboHeld;

    public const double RepeatDelay = 0.38;
    public const double RepeatInterval = 0.085;

    /// <summary>A/B tauschen (Nintendo-Layout).</summary>
    public bool SwapConfirm { get; set; }

    public event Action<NavAction>? Action;
    public event Action<int, bool>? ConnectionChanged;
    /// <summary>Home + Minus wurde 1,5 s gehalten (Spiel beenden).</summary>
    public event Action? HomeComboHeld;

    public int ConnectedCount => _connected.Count(c => c);

    /// <summary>Muss regelmäßig (z. B. 60×/s) aufgerufen werden. <paramref name="deliverActions"/> = Hub hat den Fokus.</summary>
    public void Poll(double dt, bool deliverActions)
    {
        _time += dt;
        var current = new HashSet<NavAction>();
        var anyCombo = false;
        for (int slot = 0; slot < 4; slot++)
        {
            var state = new XInputState();
            var ok = XInput.TryGetState(slot, ref state);
            if (ok != _connected[slot])
            {
                _connected[slot] = ok;
                ConnectionChanged?.Invoke(slot, ok);
            }
            if (!ok)
            {
                _prev[slot] = PadButtons.None;
                continue;
            }
            var b = (PadButtons)state.Gamepad.wButtons;
            var g = state.Gamepad;
            if (g.sThumbLY > StickThreshold) b |= PadButtons.DPadUp;
            if (g.sThumbLY < -StickThreshold) b |= PadButtons.DPadDown;
            if (g.sThumbLX < -StickThreshold) b |= PadButtons.DPadLeft;
            if (g.sThumbLX > StickThreshold) b |= PadButtons.DPadRight;
            _prev[slot] = b;

            if (b.HasFlag(PadButtons.Guide) && b.HasFlag(PadButtons.Back))
                anyCombo = true;

            void Map(PadButtons button, NavAction action)
            {
                if (b.HasFlag(button))
                    current.Add(action);
            }
            Map(PadButtons.DPadUp, NavAction.Up);
            Map(PadButtons.DPadDown, NavAction.Down);
            Map(PadButtons.DPadLeft, NavAction.Left);
            Map(PadButtons.DPadRight, NavAction.Right);
            Map(PadButtons.A, SwapConfirm ? NavAction.Back : NavAction.Accept);
            Map(PadButtons.B, SwapConfirm ? NavAction.Accept : NavAction.Back);
            Map(PadButtons.X, NavAction.X);
            Map(PadButtons.Y, NavAction.Y);
            Map(PadButtons.Start, NavAction.Plus);
            Map(PadButtons.Back, NavAction.Minus);
            Map(PadButtons.Guide, NavAction.Home);
            Map(PadButtons.LeftShoulder, NavAction.L);
            Map(PadButtons.RightShoulder, NavAction.R);
        }

        // Home + Minus halten = Spiel beenden (funktioniert auch, wenn der Hub nicht im Vordergrund ist)
        if (anyCombo)
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
                _repeatAt[action] = _time + RepeatDelay;
                if (deliverActions)
                    Action?.Invoke(action);
            }
            else if (repeatable && _time >= _repeatAt[action])
            {
                _repeatAt[action] = _time + RepeatInterval;
                if (deliverActions)
                    Action?.Invoke(action);
            }
        }
        _held.RemoveWhere(a => !current.Contains(a));
    }

    public IReadOnlyList<ControllerInfo> Controllers()
    {
        var list = new List<ControllerInfo>();
        for (int slot = 0; slot < 4; slot++)
        {
            var state = new XInputState();
            if (!XInput.TryGetState(slot, ref state))
                continue;
            var (percent, wired) = XInput.Battery(slot);
            list.Add(new ControllerInfo(slot, XInput.Describe(slot), true, percent, wired));
        }
        return list;
    }

    public void Rumble(int slot, float low, float high, int milliseconds)
    {
        XInput.SetVibration(slot, low, high);
        _ = Task.Delay(milliseconds).ContinueWith(_ => XInput.SetVibration(slot, 0, 0));
    }

    public void RumbleAll(float strength, int milliseconds)
    {
        for (int slot = 0; slot < 4; slot++)
            if (_connected[slot])
                Rumble(slot, strength, strength, milliseconds);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct XInputGamepad
{
    public ushort wButtons;
    public byte bLeftTrigger;
    public byte bRightTrigger;
    public short sThumbLX;
    public short sThumbLY;
    public short sThumbRX;
    public short sThumbRY;
}

[StructLayout(LayoutKind.Sequential)]
public struct XInputState
{
    public uint dwPacketNumber;
    public XInputGamepad Gamepad;
}

internal static class XInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputBatteryInformation
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputCapabilities
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XInputGamepad Gamepad;
        public XInputVibration Vibration;
    }

    // Ordinal 100 = XInputGetStateEx (liefert zusätzlich den Guide-Button)
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern uint XInputGetStateEx(uint index, ref XInputState state);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(uint index, ref XInputState state);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputSetState(uint index, ref XInputVibration vibration);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetBatteryInformation(uint index, byte devType, ref XInputBatteryInformation info);

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetCapabilities(uint index, uint flags, ref XInputCapabilities caps);

    private static bool _exAvailable = true;
    private static bool _available = true;

    public static bool TryGetState(int slot, ref XInputState state)
    {
        if (!_available)
            return false;
        try
        {
            if (_exAvailable)
            {
                try
                {
                    return XInputGetStateEx((uint)slot, ref state) == 0;
                }
                catch (EntryPointNotFoundException)
                {
                    _exAvailable = false;
                }
            }
            return XInputGetState((uint)slot, ref state) == 0;
        }
        catch (DllNotFoundException)
        {
            _available = false;
            return false;
        }
    }

    public static void SetVibration(int slot, float low, float high)
    {
        if (!_available)
            return;
        var v = new XInputVibration
        {
            wLeftMotorSpeed = (ushort)(Math.Clamp(low, 0, 1) * 65535),
            wRightMotorSpeed = (ushort)(Math.Clamp(high, 0, 1) * 65535),
        };
        try { XInputSetState((uint)slot, ref v); }
        catch (DllNotFoundException) { }
    }

    public static (int? percent, bool wired) Battery(int slot)
    {
        var info = new XInputBatteryInformation();
        try
        {
            if (XInputGetBatteryInformation((uint)slot, 0, ref info) != 0)
                return (null, false);
        }
        catch (DllNotFoundException)
        {
            return (null, false);
        }
        return info.BatteryType switch
        {
            1 => (null, true), // kabelgebunden
            0 => (null, false),
            _ => (info.BatteryLevel switch { 0 => 5, 1 => 30, 2 => 65, _ => 100 }, false),
        };
    }

    public static string Describe(int slot)
    {
        var caps = new XInputCapabilities();
        try
        {
            if (XInputGetCapabilities((uint)slot, 0, ref caps) == 0)
            {
                return caps.SubType switch
                {
                    1 => "XInput-Gamepad",
                    2 => "Lenkrad",
                    3 => "Arcade-Stick",
                    _ => "Controller",
                } + $" {slot + 1}";
            }
        }
        catch (DllNotFoundException)
        {
        }
        return $"Controller {slot + 1}";
    }
}
