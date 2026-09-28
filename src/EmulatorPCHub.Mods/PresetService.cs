using System.Text.Json;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Mods;

/// <summary>
/// Preset-System (Plan Abschnitt 9). Jedes Spiel kann mehrere Presets haben. Eingebaute Presets für
/// Mario Kart Wii (Vanilla / Retro Rewind / Custom) und Mario Kart 8 Deluxe (Vanilla / CTGP Deluxe / Custom);
/// eigene Presets liegen als JSON unter <c>data/presets/</c>.
/// </summary>
public sealed class PresetService
{
    private readonly AppPaths _paths;
    private readonly BackupService _backups;

    public PresetService(AppPaths paths, BackupService backups)
    {
        _paths = paths;
        _backups = backups;
    }

    public static IReadOnlyList<GamePreset> MarioKartWiiDefaults { get; } =
    [
        new()
        {
            Id = "mkwii-vanilla", Name = "Vanilla", Game = "Mario Kart Wii", Platform = "Wii", Backend = "WiiCompiled",
            Kind = PresetKind.Vanilla, IsBuiltIn = true, Description = "Original Mario Kart Wii",
            Features = ["32 Original-Strecken", "Originalspiel ohne Mods"],
        },
        new()
        {
            Id = "mkwii-retrorewind", Name = "Retro Rewind", Game = "Mario Kart Wii", Platform = "Wii", Backend = "WiiCompiled",
            Kind = PresetKind.RetroRewind, IsBuiltIn = true, Description = "Custom Tracks, Online-Funktionen, zusätzliche Inhalte",
            Features = ["Retro-Strecken aus allen Serienteilen", "Online über Retro WFC", "Zusätzliche Inhalte"],
        },
        new()
        {
            Id = "mkwii-custom", Name = "Custom", Game = "Mario Kart Wii", Platform = "Wii", Backend = "Dolphin",
            Kind = PresetKind.Custom, IsBuiltIn = true, Description = "Eigene Konfiguration: Retro Rewind mit My-Stuff-Mods",
            Features = ["Eigene Mods (My Stuff)", "Eigene Startparameter"],
        },
    ];

    public static IReadOnlyList<GamePreset> MarioKart8DeluxeDefaults { get; } =
    [
        new()
        {
            Id = "mk8dx-vanilla", Name = "Vanilla", Game = "Mario Kart 8 Deluxe", Platform = "Switch", Backend = "Switch",
            Kind = PresetKind.Vanilla, IsBuiltIn = true, Description = "Originale Version (inkl. deiner DLCs)",
            Features = ["Originalspiel", "Eigene DLCs/Updates werden geladen"],
        },
        new()
        {
            Id = "mk8dx-ctgp", Name = "CTGP Deluxe", Game = "Mario Kart 8 Deluxe", Platform = "Switch", Backend = "Switch",
            Kind = PresetKind.CtgpDeluxe, IsBuiltIn = true, Mods = ["CTGP-DX"],
            Description = "Custom Tracks, zusätzliche Cups, Mods, Erweiterungen",
            Features = ["14 neue Cups", "56 Custom Tracks", "Neuer Soundtrack"],
        },
        new()
        {
            Id = "mk8dx-custom", Name = "Custom", Game = "Mario Kart 8 Deluxe", Platform = "Switch", Backend = "Switch",
            Kind = PresetKind.Custom, IsBuiltIn = true, Description = "Eigene Mod-Konfiguration",
            Features = ["Frei wählbare Mods"],
        },
    ];

    public IReadOnlyList<GamePreset> ForGame(GameEntry game)
    {
        var builtIn = game.Special switch
        {
            SpecialPage.MarioKartWii => MarioKartWiiDefaults,
            SpecialPage.MarioKart8Deluxe => MarioKart8DeluxeDefaults,
            _ => [new GamePreset
            {
                Id = "standard", Name = "Standard", Game = game.Title, Platform = game.Platform.ShortName(),
                Backend = game.EmulatorId, Kind = PresetKind.Vanilla, IsBuiltIn = true, Description = "Normaler Start",
            }],
        };
        return builtIn.Select(p => p.Clone()).Concat(LoadCustom(game)).ToList();
    }

    public GamePreset Active(GameEntry game, string? configuredId = null)
    {
        var presets = ForGame(game);
        var id = configuredId ?? game.ActivePresetId;
        return presets.FirstOrDefault(p => p.Id == id) ?? presets[0];
    }

    private string FileFor(GameEntry game)
    {
        var safe = string.Concat(game.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
        return Path.Combine(_paths.Presets, safe + ".json");
    }

    private IEnumerable<GamePreset> LoadCustom(GameEntry game)
    {
        var file = FileFor(game);
        if (!File.Exists(file))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<GamePreset>>(File.ReadAllText(file), HubJson.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Speichert ein eigenes Preset (vorher Backup der Preset-Datei).</summary>
    public void SaveCustom(GameEntry game, GamePreset preset)
    {
        var file = FileFor(game);
        _backups.BackupFile(BackupCategory.Presets, file, $"presets-{game.Title}");
        var list = LoadCustom(game).Where(p => p.Id != preset.Id).ToList();
        preset.IsBuiltIn = false;
        list.Add(preset);
        Directory.CreateDirectory(_paths.Presets);
        File.WriteAllText(file, JsonSerializer.Serialize(list, HubJson.Options));
    }

    public void DeleteCustom(GameEntry game, string presetId)
    {
        var file = FileFor(game);
        _backups.BackupFile(BackupCategory.Presets, file, $"presets-{game.Title}");
        var list = LoadCustom(game).Where(p => p.Id != presetId).ToList();
        File.WriteAllText(file, JsonSerializer.Serialize(list, HubJson.Options));
    }
}
