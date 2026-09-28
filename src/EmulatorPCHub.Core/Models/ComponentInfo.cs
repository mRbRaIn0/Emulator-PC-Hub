namespace EmulatorPCHub.Core.Models;

public enum ComponentStatus
{
    Unknown,
    NotInstalled,
    Installed,
    UpdateAvailable,
    NeedsAttention,
}

/// <summary>Zustand einer externen Komponente (Plan Abschnitt 15).</summary>
public sealed class ComponentInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "Emulator";
    public ComponentStatus Status { get; set; }
    public string? InstalledVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? InstallPath { get; set; }
    public string? Details { get; set; }
    public string? Homepage { get; init; }
    public List<string> Problems { get; } = [];

    public string StatusText => Status switch
    {
        ComponentStatus.Installed => "Installiert ✓",
        ComponentStatus.NotInstalled => "Nicht installiert",
        ComponentStatus.UpdateAvailable => "Update verfügbar",
        ComponentStatus.NeedsAttention => "Prüfen",
        _ => "Unbekannt",
    };
}

/// <summary>Mod-Eintrag für den Mod Manager (Plan Abschnitt 13).</summary>
public sealed class ModInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Spiel-Schlüssel (Title-ID oder Disc-ID).</summary>
    public required string GameKey { get; init; }
    public string GameTitle { get; init; } = "";
    public string? Version { get; set; }
    public bool Enabled { get; set; }
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Source { get; init; } = "Hub";
    public List<string> Dependencies { get; init; } = [];
}
