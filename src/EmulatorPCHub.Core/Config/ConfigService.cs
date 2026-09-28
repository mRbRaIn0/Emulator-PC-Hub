using System.Text.Json;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Core.Config;

/// <summary>Lädt und speichert <see cref="HubConfig"/> als JSON.</summary>
public sealed class ConfigService
{
    private readonly AppPaths _paths;
    private readonly BackupService? _backups;
    private readonly object _lock = new();

    public HubConfig Current { get; private set; } = new();

    public event EventHandler? Changed;

    public ConfigService(AppPaths paths, BackupService? backups = null)
    {
        _paths = paths;
        _backups = backups;
    }

    public HubConfig Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_paths.ConfigFile))
                {
                    var json = File.ReadAllText(_paths.ConfigFile);
                    Current = JsonSerializer.Deserialize<HubConfig>(json, HubJson.Options) ?? new HubConfig();
                }
                else
                {
                    Current = new HubConfig();
                }
            }
            catch (Exception ex)
            {
                HubLog.Error("Konfiguration konnte nicht gelesen werden, Standardwerte werden verwendet.", ex);
                // Defekte Datei sichern, damit nichts verloren geht.
                _backups?.BackupFile(BackupCategory.Config, _paths.ConfigFile, "config-defekt");
                Current = new HubConfig();
            }
            return Current;
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.ConfigFile)!);
            var tmp = _paths.ConfigFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, HubJson.Options));
            File.Move(tmp, _paths.ConfigFile, overwrite: true);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<HubConfig> change)
    {
        change(Current);
        Save();
    }
}

public static class HubJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
