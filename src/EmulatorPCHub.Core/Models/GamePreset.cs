using System.Text.Json.Serialization;

namespace EmulatorPCHub.Core.Models;

/// <summary>
/// Ein Preset (Plan Abschnitt 9). Das JSON-Format entspricht dem Plan:
/// name, game, platform, backend, mods, arguments, cover, banner.
/// </summary>
public sealed class GamePreset
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    [JsonPropertyName("platform")] public string Platform { get; set; } = "";
    [JsonPropertyName("backend")] public string Backend { get; set; } = "";
    [JsonPropertyName("mods")] public List<string> Mods { get; set; } = [];
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = "";
    [JsonPropertyName("cover")] public string Cover { get; set; } = "";
    [JsonPropertyName("banner")] public string Banner { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("features")] public List<string> Features { get; set; } = [];
    [JsonPropertyName("kind")] public PresetKind Kind { get; set; } = PresetKind.Vanilla;
    [JsonPropertyName("builtIn")] public bool IsBuiltIn { get; set; }

    public GamePreset Clone() => new()
    {
        Id = Id, Name = Name, Game = Game, Platform = Platform, Backend = Backend,
        Mods = [.. Mods], Arguments = Arguments, Cover = Cover, Banner = Banner,
        Description = Description, Features = [.. Features], Kind = Kind, IsBuiltIn = IsBuiltIn,
    };
}

[JsonConverter(typeof(JsonStringEnumConverter<PresetKind>))]
public enum PresetKind
{
    /// <summary>Originalspiel ohne Mods.</summary>
    Vanilla,
    /// <summary>Retro Rewind (Mario Kart Wii).</summary>
    RetroRewind,
    /// <summary>CTGP Deluxe (Mario Kart 8 Deluxe).</summary>
    CtgpDeluxe,
    /// <summary>Eigene Mod-Zusammenstellung.</summary>
    Custom,
}
