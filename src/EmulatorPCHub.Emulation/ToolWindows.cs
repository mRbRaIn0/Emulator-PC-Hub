using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace EmulatorPCHub.Emulation;

/// <summary>
/// Win32-Hilfen für Hilfsfenster von Emulatoren (z. B. Skylanders-Portal): Fenster eines Prozesses finden,
/// beim ersten Mal auf einen anderen Bildschirm als das Spiel legen und die vom Nutzer gewählte Position merken.
/// </summary>
public static class ToolWindows
{
    public static List<IntPtr> TopWindows(int pid)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out var p);
            if (p == pid && IsWindowVisible(h))
                list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static string Title(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>Wartet auf ein neues sichtbares Fenster des Prozesses (nicht in <paramref name="before"/>).</summary>
    public static async Task<IntPtr> WaitForNewWindowAsync(Process process, ICollection<IntPtr> before, TimeSpan timeout, CancellationToken ct)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until && !process.HasExited)
        {
            await Task.Delay(100, ct);
            var found = TopWindows(process.Id).FirstOrDefault(h => !before.Contains(h));
            if (found != IntPtr.Zero)
                return found;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Legt das Fenster an die gemerkte Position (sonst auf einen anderen Bildschirm als <paramref name="gameWindow"/>)
    /// und merkt sich jede Verschiebung, solange Fenster und Prozess leben.
    /// </summary>
    public static async Task PlaceAndTrackAsync(Process process, IntPtr window, IntPtr gameWindow, string rectFile, CancellationToken ct)
    {
        var saved = LoadRect(rectFile);
        Place(window, gameWindow, saved);
        while (!process.HasExited && !ct.IsCancellationRequested && IsWindow(window))
        {
            if (IsWindowVisible(window) && !IsIconic(window) && GetWindowRect(window, out var r) && !r.Equals(saved))
            {
                saved = r;
                try { File.WriteAllText(rectFile, $"{r.Left};{r.Top};{r.Right};{r.Bottom}"); }
                catch (Exception) { }
            }
            await Task.Delay(1500, ct);
        }
    }

    private static RECT LoadRect(string file)
    {
        try
        {
            var p = File.ReadAllText(file).Trim().Split(';').Select(int.Parse).ToArray();
            return new RECT { Left = p[0], Top = p[1], Right = p[2], Bottom = p[3] };
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static void Place(IntPtr window, IntPtr gameWindow, RECT saved)
    {
        const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
        if (saved.Right > saved.Left && MonitorFromRect(ref saved, 0) != IntPtr.Zero)
        {
            SetWindowPos(window, IntPtr.Zero, saved.Left, saved.Top, saved.Right - saved.Left, saved.Bottom - saved.Top, SWP_NOZORDER | SWP_NOACTIVATE);
            return;
        }
        // Erstes Mal: auf einen anderen Bildschirm als das Spiel (falls vorhanden)
        var gameMonitor = MonitorFromWindow(gameWindow, 2);
        var other = Monitors().FirstOrDefault(m => m.handle != gameMonitor);
        if (other.handle == IntPtr.Zero || !GetWindowRect(window, out var r))
            return;
        var (w, h) = (r.Right - r.Left, r.Bottom - r.Top);
        var work = other.work;
        var x = work.Left + Math.Max(0, (work.Right - work.Left - w) / 2);
        var y = work.Top + Math.Max(0, (work.Bottom - work.Top - h) / 2);
        SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE);
    }

    /// <summary>
    /// Tastenkombination an ein Fenster senden (Fenster wird dafür in den Vordergrund geholt). Die Tasten bleiben
    /// kurz gedrückt, weil Emulatoren ihre Hotkeys nur in Abständen abfragen.
    /// </summary>
    public static async Task SendHotkeyAsync(IntPtr window, params ushort[] virtualKeys)
    {
        SetForegroundWindow(window);
        var down = virtualKeys.Select(vk => Key(vk, up: false)).ToArray();
        var up = virtualKeys.Reverse().Select(vk => Key(vk, up: true)).ToArray();
        SendInput((uint)down.Length, down, Marshal.SizeOf<INPUT>());
        await Task.Delay(150);
        SendInput((uint)up.Length, up, Marshal.SizeOf<INPUT>());
    }

    public static bool IsForeground(IntPtr window) => GetForegroundWindow() == window;

    // Mit Scan-Code: Emulatoren lesen die Tastatur oft per DirectInput/Raw Input, das nur Scan-Codes auswertet.
    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = 1,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = (ushort)MapVirtualKey(vk, 0),
                dwFlags = 0x0008u /* KEYEVENTF_SCANCODE */ | (up ? 0x0002u /* KEYEVENTF_KEYUP */ : 0u),
            },
        },
    };

    private static List<(IntPtr handle, RECT work)> Monitors()
    {
        var list = new List<(IntPtr, RECT)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (m, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(m, ref info))
                list.Add((m, info.rcWork));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
}
