using System.Text.Json;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.UI.Services;

/// <summary>
/// Was eine Konsolen-Taste im jeweiligen Spiel macht („A“ → „Springen“). Eigene Einträge liegen in
/// <c>data/game-controls.json</c>; für einige bekannte Spiele gibt es Vorschläge, die man überschreiben kann.
/// Schlüssel sind emulierter Controller + Zielname aus <see cref="Emulation.Input.ButtonMap"/> (z. B. „Wiimote/A“),
/// denn „B“ am Wii Remote bedeutet im selben Spiel etwas anderes als „B“ am GameCube-Controller.
/// </summary>
public sealed class GameControlsStore
{
    private readonly string _file;
    private readonly object _lock = new();
    private Dictionary<string, Dictionary<string, string>> _data;

    public GameControlsStore(AppPaths paths)
    {
        _file = Path.Combine(paths.Data, "game-controls.json");
        _data = Load();
    }

    /// <summary>Funktion einer Taste im Spiel: eigener Eintrag, sonst Vorschlag, sonst null.</summary>
    public (string? text, bool suggestion) Get(GameEntry game, EmulatedPad pad, string target)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(game.Id, out var own) && own.TryGetValue(Key(pad, target), out var text))
                return (text.Length == 0 ? null : text, false);
        }
        return KnownControls.For(game, pad)?.GetValueOrDefault(target) is { } s ? (s, true) : (null, false);
    }

    private static string Key(EmulatedPad pad, string target) => $"{pad}/{target}";

    /// <summary>Eigenen Eintrag setzen; leerer Text = „nicht belegt“ (überschreibt auch einen Vorschlag).</summary>
    public void Set(GameEntry game, EmulatedPad pad, string target, string text)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(game.Id, out var own))
                _data[game.Id] = own = [];
            own[Key(pad, target)] = text.Trim();
        }
        Save();
    }

    /// <summary>Eigenen Eintrag entfernen (danach gilt wieder der Vorschlag).</summary>
    public void Reset(GameEntry game, EmulatedPad pad, string target)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(game.Id, out var own))
                own.Remove(Key(pad, target));
        }
        Save();
    }

    public bool HasSuggestions(GameEntry game, EmulatedPad pad) => KnownControls.For(game, pad) != null;

    private void Save()
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_data, HubJson.Options));
        }
    }

    private Dictionary<string, Dictionary<string, string>> Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(_file), HubJson.Options) ?? [];
        }
        catch (JsonException)
        {
        }
        return [];
    }
}

/// <summary>Vorschläge für die Steuerung bekannter Spiele (Standardsteuerung laut Spiel).</summary>
public static class KnownControls
{
    public static IReadOnlyDictionary<string, string>? For(GameEntry game, EmulatedPad pad)
    {
        var t = game.Title;
        var modern = pad is EmulatedPad.WiiUPro or EmulatedPad.WiiUGamePad or EmulatedPad.SwitchPro or EmulatedPad.SwitchJoyConPair;
        if (modern && t.Contains("Breath of the Wild", StringComparison.OrdinalIgnoreCase))
            return BreathOfTheWild;
        if (modern && (game.Special == SpecialPage.MarioKart8Deluxe || t.Contains("Mario Kart 8", StringComparison.OrdinalIgnoreCase)))
            return MarioKart8;
        if (game.Special == SpecialPage.MarioKartWii || t.Contains("Mario Kart Wii", StringComparison.OrdinalIgnoreCase))
            return pad switch
            {
                EmulatedPad.Wiimote => MarioKartWiiRemote,
                EmulatedPad.GameCube => MarioKartWiiGameCube,
                _ => null,
            };
        return null;
    }

    private static readonly Dictionary<string, string> BreathOfTheWild = new()
    {
        ["A"] = "Aktion: sprechen, aufheben, öffnen",
        ["B"] = "Sprinten / abbrechen",
        ["X"] = "Springen",
        ["Y"] = "Angreifen",
        ["ZR"] = "Bogen spannen / schießen",
        ["ZL"] = "Anvisieren / Schild",
        ["R"] = "Waffe werfen",
        ["L"] = "Shiekah-Stein-Modul benutzen",
        ["+"] = "Menü (Inventar)",
        ["−"] = "Karte",
        ["Linker Stick"] = "Laufen",
        ["Rechter Stick"] = "Kamera",
        ["Linken Stick drücken"] = "Schleichen",
        ["Rechten Stick drücken"] = "Fernrohr",
        ["Steuerkreuz ↑"] = "Module auswählen",
        ["Steuerkreuz ↓"] = "Pfeifen (Pferd rufen)",
        ["Steuerkreuz ←"] = "Schild wechseln",
        ["Steuerkreuz →"] = "Waffe wechseln",
    };

    private static readonly Dictionary<string, string> MarioKart8 = new()
    {
        ["A"] = "Gas geben",
        ["B"] = "Bremsen / rückwärts",
        ["R"] = "Springen / driften",
        ["ZR"] = "Springen / driften",
        ["L"] = "Item benutzen",
        ["ZL"] = "Item benutzen",
        ["X"] = "Nach hinten schauen",
        ["+"] = "Pause",
        ["Linker Stick"] = "Lenken",
    };

    private static readonly Dictionary<string, string> MarioKartWiiRemote = new()
    {
        ["A"] = "Gas geben",
        ["B"] = "Springen / driften / bremsen",
        ["+"] = "Pause",
        ["Nunchuk-Stick"] = "Lenken",
        ["Wii Remote schütteln"] = "Trick (beim Sprung)",
    };

    private static readonly Dictionary<string, string> MarioKartWiiGameCube = new()
    {
        ["A"] = "Gas geben",
        ["B"] = "Bremsen / rückwärts",
        ["R"] = "Springen / driften",
        ["L"] = "Item benutzen",
        ["Control-Stick"] = "Lenken",
        ["Start/Pause"] = "Pause",
    };
}
