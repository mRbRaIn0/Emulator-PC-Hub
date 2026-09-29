namespace EmulatorPCHub.Core.Models;

/// <summary>Spiele mit eigener Spezialseite im Hub.</summary>
public enum SpecialPage
{
    None,
    MarioKartWii,
    MarioKart8Deluxe,
    HubKart,
}

/// <summary>Sprachvarianten eines Spiels (Codes wie "de", "en", "ja").</summary>
public sealed class GameLanguageData
{
    /// <summary>Sprachen, die das Spiel laut Datei/Metadaten mitbringt.</summary>
    public List<string> Detected { get; set; } = [];

    /// <summary>Aus den Metadaten gelesene Titel (bei jedem Scan neu gesetzt).</summary>
    public Dictionary<string, string> DetectedTitles { get; set; } = [];

    /// <summary>Vom Nutzer vergebene Titel.</summary>
    public Dictionary<string, string> Titles { get; set; } = [];

    /// <summary>Eigene Cover pro Sprache (Dateipfade).</summary>
    public Dictionary<string, string> Covers { get; set; } = [];

    /// <summary>Alle für dieses Spiel wählbaren Sprachen: erkannte + Erst-/Zweitsprache + bereits angepasste.</summary>
    public List<string> Available()
    {
        var list = new List<string>();
        foreach (var l in HubLanguage.Preferred().Concat(Detected).Concat(DetectedTitles.Keys).Concat(Titles.Keys).Concat(Covers.Keys))
            if (!list.Contains(l))
                list.Add(l);
        return list;
    }
}

/// <summary>Ein Bibliothekseintrag (Plan Abschnitt 11).</summary>
public sealed class GameEntry
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public HubPlatform Platform { get; set; }

    /// <summary>Pfad zum eigenen Dump (leer bei eingebauten Spielen).</summary>
    public string Path { get; set; } = "";

    /// <summary>Disc-ID (z. B. RMCP01) bzw. Title-ID (z. B. 0100152000022000).</summary>
    public string? GameCode { get; set; }

    public string? CoverPath { get; set; }
    public string? BackgroundPath { get; set; }
    public string? IconPath { get; set; }

    /// <summary>ID des Emulator-Adapters (siehe <see cref="EmulatorIds"/>).</summary>
    public string EmulatorId { get; set; } = EmulatorIds.BuiltIn;

    public string? ActivePresetId { get; set; }
    public DateTimeOffset? LastPlayed { get; set; }
    public long PlayTimeSeconds { get; set; }
    public bool IsFavorite { get; set; }
    public string? ControllerProfile { get; set; }
    public string? GraphicsProfile { get; set; }
    public string? SavePath { get; set; }
    public SpecialPage Special { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public long FileSize { get; set; }

    /// <summary>Name wurde vom Nutzer geändert – der Bibliotheks-Scan überschreibt ihn nicht.</summary>
    public bool CustomTitle { get; set; }

    /// <summary>Nicht auf dem Homescreen anzeigen (bleibt unter „Alle Spiele“ sichtbar).</summary>
    public bool HiddenOnHome { get; set; }

    /// <summary>Eintrag existiert nur als Platzhalter (z. B. Mario Kart Wii ohne erkannten Dump).</summary>
    public bool IsPlaceholder { get; set; }

    /// <summary>Titel/Cover pro Sprache (erkannt und vom Nutzer gesetzt).</summary>
    public GameLanguageData Languages { get; set; } = new();

    /// <summary>Angezeigter Titel gemäß Erst-/Zweitsprache; ohne passende Variante der normale <see cref="Title"/>.</summary>
    public string DisplayTitle
    {
        get
        {
            foreach (var lang in HubLanguage.Preferred())
            {
                if (Languages.Titles.TryGetValue(lang, out var custom) && !string.IsNullOrWhiteSpace(custom))
                    return custom;
                if (!CustomTitle && Languages.DetectedTitles.TryGetValue(lang, out var detected) && !string.IsNullOrWhiteSpace(detected))
                    return detected;
            }
            return Title;
        }
    }

    /// <summary>Angezeigtes Cover gemäß Erst-/Zweitsprache; ohne passende Variante das normale <see cref="CoverPath"/>.</summary>
    public string? DisplayCoverPath
    {
        get
        {
            foreach (var lang in HubLanguage.Preferred())
                if (Languages.Covers.TryGetValue(lang, out var cover) && File.Exists(cover))
                    return cover;
            return CoverPath;
        }
    }

    public TimeSpan PlayTime => TimeSpan.FromSeconds(PlayTimeSeconds);

    public override string ToString() => $"{Title} ({Platform.ShortName()})";
}

/// <summary>Bekannte Spiele-IDs für die Spezialintegrationen.</summary>
public static class KnownGames
{
    public const string HubKartId = "builtin:hubkart";
    public const string MarioKartWiiId = "special:mkwii";
    public const string MarioKart8DeluxeId = "special:mk8dx";

    /// <summary>Title-ID von Mario Kart 8 Deluxe.</summary>
    public const string MarioKart8DeluxeTitleId = "0100152000022000";

    public static bool IsMarioKartWiiCode(string? code) =>
        code is { Length: >= 4 } && code.StartsWith("RMC", StringComparison.OrdinalIgnoreCase);

    public static bool IsMarioKart8DeluxeCode(string? code) =>
        string.Equals(code, MarioKart8DeluxeTitleId, StringComparison.OrdinalIgnoreCase);
}
