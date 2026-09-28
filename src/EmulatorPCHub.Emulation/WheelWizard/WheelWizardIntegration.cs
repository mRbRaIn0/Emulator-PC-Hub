using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Emulation.WheelWizard;

public sealed record WheelWizardConfig(string? DolphinLocation, string? UserFolderPath, string? GameLocation, string? ConfigFile);

/// <summary>
/// Wheel Wizard (Plan 6.3): wird für Installation/Updates von Retro Rewind und WiiCompiled genutzt.
/// Im Normalbetrieb muss der Benutzer Wheel Wizard nicht öffnen – der Hub liest und schreibt dessen Konfiguration.
/// Über „Advanced Tools → Wheel Wizard öffnen“ bleibt es erreichbar.
/// </summary>
public sealed class WheelWizardIntegration
{
    private readonly AppPaths _paths;
    private readonly ConfigService _config;
    private readonly BackupService? _backups;

    public WheelWizardIntegration(AppPaths paths, ConfigService config, BackupService? backups = null)
    {
        _paths = paths;
        _config = config;
        _backups = backups;
    }

    public string? FindExecutable()
    {
        foreach (var c in new[]
                 {
                     _config.Current.Emulators.WheelWizard,
                     Path.Combine(_paths.IntegrationDir("wheelwizard"), "WheelWizard.exe"),
                     Path.Combine(_paths.IntegrationDir("wheelwizard"), "WheelWizardWindows.exe"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WheelWizard", "WheelWizard.exe"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return c;
        }
        return null;
    }

    public string? Version()
    {
        var exe = FindExecutable();
        if (exe == null)
            return null;
        var marker = Path.Combine(Path.GetDirectoryName(exe)!, AdapterBase.ComponentVersionFile);
        if (File.Exists(marker))
            return File.ReadAllText(marker).Trim();
        try
        {
            var v = FileVersionInfo.GetVersionInfo(exe);
            var s = v.ProductVersion ?? v.FileVersion;
            return s?.Split('+')[0];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Datenordner von Wheel Wizard (portabel: neben der EXE, sonst %APPDATA%\CT-MKWII).</summary>
    public string DataFolder()
    {
        var exe = FindExecutable();
        if (exe != null)
        {
            var dir = Path.GetDirectoryName(exe)!;
            if (File.Exists(Path.Combine(dir, "portable-ww.txt")))
                return Path.Combine(dir, "CT-MKWII");
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CT-MKWII");
    }

    public string ConfigFile => Path.Combine(DataFolder(), "config.json");

    public WheelWizardConfig ReadConfig()
    {
        var file = ConfigFile;
        if (!File.Exists(file))
            return new WheelWizardConfig(null, null, null, null);
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
            string? S(string k) => node?[k]?.GetValueKind() == JsonValueKind.String ? node[k]!.GetValue<string>() : null;
            return new WheelWizardConfig(S("DolphinLocation"), S("UserFolderPath"), S("GameLocation"), file);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Wheel-Wizard-Konfiguration nicht lesbar", ex);
            return new WheelWizardConfig(null, null, null, file);
        }
    }

    /// <summary>
    /// Trägt Dolphin, Userordner und Spiel in Wheel Wizard ein, damit es ohne Ersteinrichtung funktioniert.
    /// Vorher wird die Datei gesichert.
    /// </summary>
    public void WriteConfig(string? dolphinExe, string? userFolder, string? gameFile)
    {
        var file = ConfigFile;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        JsonObject node;
        try
        {
            node = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? new JsonObject() : new JsonObject();
        }
        catch (JsonException)
        {
            node = new JsonObject();
        }
        string? Current(string key) => node[key]?.GetValueKind() == JsonValueKind.String ? node[key]!.GetValue<string>() : null;
        bool Differs(string key, string? value) => !string.IsNullOrWhiteSpace(value) && !string.Equals(Current(key), value, StringComparison.OrdinalIgnoreCase);
        if (!Differs("DolphinLocation", dolphinExe) && !Differs("UserFolderPath", userFolder) && !Differs("GameLocation", gameFile))
            return; // nichts zu ändern
        _backups?.BackupFile(BackupCategory.Config, file, "wheelwizard-config");
        if (!string.IsNullOrWhiteSpace(dolphinExe))
            node["DolphinLocation"] = dolphinExe;
        if (!string.IsNullOrWhiteSpace(userFolder))
            node["UserFolderPath"] = userFolder;
        if (!string.IsNullOrWhiteSpace(gameFile))
            node["GameLocation"] = gameFile;
        File.WriteAllText(file, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        HubLog.Info("Wheel-Wizard-Konfiguration aktualisiert");
    }

    /// <summary>Advanced Tools → Wheel Wizard öffnen.</summary>
    public bool Open()
    {
        var exe = FindExecutable();
        if (exe == null)
            return false;
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
        return true;
    }
}
