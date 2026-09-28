using System.Text.Json;
using System.Text.Json.Serialization;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Emulation.Input;

namespace EmulatorPCHub.UI.Services;

/// <summary>Controller-Profile pro Spiel und die gemerkte Spieler-Zuordnung (data/controllers.json).</summary>
public sealed class ControllerProfileStore
{
    private sealed class FileModel
    {
        [JsonPropertyName("assignments")] public Dictionary<string, int> Assignments { get; set; } = [];
        [JsonPropertyName("profiles")] public List<ControllerProfile> Profiles { get; set; } = [];
        [JsonPropertyName("useCompatibilityLayer")] public bool UseCompatibilityLayer { get; set; } = true;
        [JsonPropertyName("realWiimotesInFreeSlots")] public bool RealWiimotesInFreeSlots { get; set; } = true;
        [JsonPropertyName("confirmWithSouth")] public bool ConfirmWithSouth { get; set; } = true;
    }

    private readonly string _file;
    private readonly object _lock = new();
    private FileModel _data;

    public ControllerProfileStore(AppPaths paths)
    {
        _file = Path.Combine(paths.Data, "controllers.json");
        _data = Load();
        ApplyConfirmSouthToAll();
    }

    /// <summary>PadForge automatisch verwenden, wenn ein Spiel einen Controller nicht direkt unterstützt.</summary>
    public bool UseCompatibilityLayer
    {
        get => _data.UseCompatibilityLayer;
        set
        {
            _data.UseCompatibilityLayer = value;
            Save();
        }
    }

    /// <summary>Wii-Spiele (Dolphin): freie Spielerplätze mit echten Wii Remotes belegen.</summary>
    public bool RealWiimotesInFreeSlots
    {
        get => _data.RealWiimotesInFreeSlots;
        set
        {
            _data.RealWiimotesInFreeSlots = value;
            Save();
        }
    }

    /// <summary>
    /// PlayStation-/Xbox-Controller: ✕ bzw. A (untere Taste) bestätigt, ○ bzw. B (rechte Taste) bricht ab – auch in
    /// Wii-U-, Switch-, DS- und 3DS-Spielen, die sonst nach Nintendo-Position belegt würden. Gilt für alle Profile.
    /// </summary>
    public bool ConfirmWithSouth
    {
        get => _data.ConfirmWithSouth;
        set
        {
            _data.ConfirmWithSouth = value;
            ApplyConfirmSouthToAll();
            Save();
        }
    }

    private void ApplyConfirmSouthToAll()
    {
        lock (_lock)
        {
            if (_data.Profiles.All(p => p.GameKey != "default"))
                _data.Profiles.Add(new ControllerProfile { GameKey = "default" });
            foreach (var p in _data.Profiles)
                ButtonMap.ApplyConfirmSouth(p.Remap, _data.ConfirmWithSouth);
        }
    }

    public int? GetAssignment(string key)
    {
        lock (_lock)
            return _data.Assignments.TryGetValue(key, out var p) ? p : null;
    }

    public void SetAssignment(string key, int player)
    {
        lock (_lock)
            _data.Assignments[key] = player;
        Save();
    }

    public ControllerProfile Default => GetOrCreate("default");

    /// <summary>Profil für ein Spiel – existiert keins, gilt das Standardprofil.</summary>
    public ControllerProfile For(string gameId)
    {
        lock (_lock)
            return _data.Profiles.FirstOrDefault(p => p.GameKey == gameId) ?? Default;
    }

    public bool HasOwnProfile(string gameId)
    {
        lock (_lock)
            return _data.Profiles.Any(p => p.GameKey == gameId);
    }

    public ControllerProfile GetOrCreate(string gameId)
    {
        lock (_lock)
        {
            var p = _data.Profiles.FirstOrDefault(x => x.GameKey == gameId);
            if (p != null)
                return p;
            var template = _data.Profiles.FirstOrDefault(x => x.GameKey == "default");
            p = template == null
                ? new ControllerProfile { GameKey = gameId }
                : JsonSerializer.Deserialize<ControllerProfile>(JsonSerializer.Serialize(template))!;
            p.GameKey = gameId;
            _data.Profiles.Add(p);
            return p;
        }
    }

    public void Remove(string gameId)
    {
        lock (_lock)
            _data.Profiles.RemoveAll(p => p.GameKey == gameId && gameId != "default");
        Save();
    }

    public void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_data, HubJson.Options));
        }
    }

    private FileModel Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<FileModel>(File.ReadAllText(_file), HubJson.Options) ?? new FileModel();
        }
        catch (JsonException)
        {
        }
        return new FileModel();
    }
}
