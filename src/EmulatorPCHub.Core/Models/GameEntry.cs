namespace EmulatorPCHub.Core.Models;

/// <summary>Spiele mit eigener Spezialseite im Hub.</summary>
public enum SpecialPage
{
    None,
    MarioKartWii,
    MarioKart8Deluxe,
    HubKart,
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
