using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.App.Services;

/// <summary>Power-Menü (Plan Abschnitt 20), Autostart, Startmenü-Verknüpfung, Ordner/Links öffnen.</summary>
public static class SystemService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "EmulatorPCHub";

    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Emulator PC Hub.exe");

    public static void Sleep() => SetSuspendState(false, false, false);

    public static void Restart() => RunHidden("shutdown.exe", "/r /t 0");

    public static void Shutdown() => RunHidden("shutdown.exe", "/s /t 0");

    public static void Lock() => LockWorkStation();

    private static void RunHidden(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            HubLog.Error($"{exe} {args} fehlgeschlagen", ex);
        }
    }

    /// <summary>Windows-Login → Hub automatisch im Console Mode starten (Plan Abschnitt 19).</summary>
    public static bool Autostart
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(RunValue, $"\"{ExePath}\" --console");
            else
                key.DeleteValue(RunValue, throwOnMissingValue: false);
            HubLog.Info($"Autostart {(value ? "aktiviert" : "deaktiviert")}");
        }
    }

    /// <summary>Legt eine Startmenü-Verknüpfung an (Installations-Assistent Schritt 8).</summary>
    public static string? CreateStartMenuShortcut()
    {
        try
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var link = Path.Combine(programs, "Emulator PC Hub.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(link);
            shortcut.TargetPath = ExePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(ExePath);
            shortcut.IconLocation = Path.Combine(AppContext.BaseDirectory, "Assets", "hub.ico");
            shortcut.Description = "Emulator PC Hub";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            HubLog.Info($"Startmenü-Verknüpfung erstellt: {link}");
            return link;
        }
        catch (Exception ex)
        {
            HubLog.Error("Startmenü-Verknüpfung konnte nicht erstellt werden", ex);
            return null;
        }
    }

    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (File.Exists(path))
            path = Path.GetDirectoryName(path);
        if (path != null && Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    /// <summary>Öffnet den Explorer und markiert die Datei.</summary>
    public static void ShowInExplorer(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
            return;
        if (File.Exists(file))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        else
            OpenFolder(file);
    }

    public static void OpenUrl(string url)
    {
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static bool IsOnline()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            return false;
        }
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
}
