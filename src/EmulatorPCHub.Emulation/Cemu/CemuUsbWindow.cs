using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Emulation.Cemu;

/// <summary>
/// Öffnet in Cemu das Fenster „Emulated USB Devices“ (Skylanders-Portal) automatisch über den Menüeintrag
/// und legt es dort ab, wo der Nutzer es zuletzt hatte – sonst auf einen anderen Bildschirm als das Spiel.
/// Cemu hat dafür keine Startoption; der Menüeintrag wird per Win32 im Menü von Cemu gesucht.
/// </summary>
public static class CemuUsbWindow
{
    private const uint WM_COMMAND = 0x0111;
    /// <summary>ID von „Tools → Emulated USB Devices“ in Cemu 2.6 (im Vollbild ist das Menü nicht auslesbar).</summary>
    private const uint KnownMenuId = 20603;

    /// <param name="stateDir">Ordner für gemerkte Menü-ID und Fensterposition.</param>
    public static async Task OpenAsync(Process process, string stateDir, CancellationToken ct)
    {
        try
        {
            var menuFile = Path.Combine(stateDir, "hub-usb-menu.txt");
            var exeStamp = File.GetLastWriteTimeUtc(process.MainModule?.FileName ?? "").Ticks;
            var cached = LoadMenuId(menuFile, exeStamp);

            // Hauptfenster mit Menü abwarten (im Vollbild wird das Menü abgehängt – dann gilt die gemerkte ID)
            IntPtr main = IntPtr.Zero;
            uint? menuId = null;
            for (var i = 0; i < 300 && !process.HasExited && !ct.IsCancellationRequested; i++)
            {
                foreach (var hwnd in ToolWindows.TopWindows(process.Id))
                {
                    var menu = GetMenu(hwnd);
                    if (menu != IntPtr.Zero && FindUsbItem(menu) is { } id)
                    {
                        (main, menuId) = (hwnd, id);
                        break;
                    }
                    if (main == IntPtr.Zero)
                        main = hwnd;
                }
                if (menuId != null || (main != IntPtr.Zero && i >= 25))
                    break;
                await Task.Delay(200, ct);
            }
            if (main == IntPtr.Zero)
                return;
            menuId ??= cached ?? KnownMenuId;
            try { File.WriteAllText(menuFile, $"{exeStamp};{menuId}"); }
            catch (Exception) { }

            var before = ToolWindows.TopWindows(process.Id);
            PostMessage(main, WM_COMMAND, (IntPtr)menuId.Value, IntPtr.Zero);
            var usb = await ToolWindows.WaitForNewWindowAsync(process, before, TimeSpan.FromSeconds(5), ct);
            if (usb == IntPtr.Zero)
            {
                HubLog.Warn("Cemu: Fenster „Emulated USB Devices“ ging nicht auf – bitte über Tools öffnen.");
                return;
            }
            await ToolWindows.PlaceAndTrackAsync(process, usb, main, Path.Combine(stateDir, "hub-usb-window.txt"), ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HubLog.Warn("Cemu: USB-Fenster konnte nicht automatisch geöffnet werden", ex);
        }
    }

    private static uint? LoadMenuId(string file, long exeStamp)
    {
        try
        {
            var parts = File.ReadAllText(file).Trim().Split(';');
            return long.Parse(parts[0]) == exeStamp ? uint.Parse(parts[1]) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Menüeintrag, dessen Text „USB“ enthält (sprachunabhängig: „Emulated USB Devices“ / „Emulierte USB-Geräte“).</summary>
    private static uint? FindUsbItem(IntPtr menu)
    {
        var count = GetMenuItemCount(menu);
        for (var i = 0; i < count; i++)
        {
            var sub = GetSubMenu(menu, i);
            if (sub != IntPtr.Zero)
            {
                if (FindUsbItem(sub) is { } found)
                    return found;
                continue;
            }
            var sb = new StringBuilder(256);
            GetMenuString(menu, (uint)i, sb, sb.Capacity, 0x0400 /* MF_BYPOSITION */);
            var id = GetMenuItemID(menu, i);
            if (id != uint.MaxValue && sb.ToString().Contains("USB", StringComparison.OrdinalIgnoreCase))
                return id;
        }
        return null;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] private static extern IntPtr GetSubMenu(IntPtr menu, int pos);
    [DllImport("user32.dll")] private static extern uint GetMenuItemID(IntPtr menu, int pos);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMenuString(IntPtr menu, uint item, StringBuilder text, int max, uint flags);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
