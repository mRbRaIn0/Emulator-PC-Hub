using System.Diagnostics;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Emulation.Dolphin;

/// <summary>
/// Öffnet in Dolphin das Skylanders-Portal-Fenster automatisch. Im Batch-Modus (-b) gibt es kein Menü,
/// daher wird Dolphins Standard-Hotkey „Show Skylanders Portal“ (Strg+P) an das Spielfenster gesendet.
/// </summary>
public static class DolphinPortalWindow
{
    private const ushort VK_CONTROL = 0x11, VK_P = 0x50;

    /// <param name="stateDir">Ordner für die gemerkte Fensterposition.</param>
    public static async Task OpenAsync(Process process, string stateDir, CancellationToken ct)
    {
        try
        {
            // Spielfenster abwarten, dann dem Kern kurz Zeit zum Hochfahren geben (Hotkeys wirken erst im laufenden Spiel)
            IntPtr game = IntPtr.Zero;
            for (var i = 0; i < 300 && game == IntPtr.Zero && !process.HasExited; i++)
            {
                await Task.Delay(200, ct);
                game = ToolWindows.TopWindows(process.Id).FirstOrDefault();
            }
            if (game == IntPtr.Zero)
                return;
            await Task.Delay(3000, ct);

            for (var attempt = 0; attempt < 5 && !process.HasExited; attempt++)
            {
                var before = ToolWindows.TopWindows(process.Id);
                if (!before.Contains(game))
                    game = before.FirstOrDefault();
                if (game == IntPtr.Zero)
                    return;
                await ToolWindows.SendHotkeyAsync(game, VK_CONTROL, VK_P);
                var portal = await ToolWindows.WaitForNewWindowAsync(process, before, TimeSpan.FromSeconds(3), ct);
                if (portal != IntPtr.Zero)
                {
                    await ToolWindows.PlaceAndTrackAsync(process, portal, game, Path.Combine(stateDir, "hub-skylanders-window.txt"), ct);
                    return;
                }
                await Task.Delay(2000, ct);
            }
            HubLog.Warn("Dolphin: Skylanders-Portal-Fenster ging nicht auf – im Spiel Strg+P drücken.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HubLog.Warn("Dolphin: Skylanders-Portal-Fenster konnte nicht automatisch geöffnet werden", ex);
        }
    }
}
