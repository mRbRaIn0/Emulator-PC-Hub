using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>Art einer Switch-Datei (anhand der Title-ID).</summary>
public enum SwitchContentKind
{
    Unknown,
    Base,
    Update,
    Dlc,
}

/// <summary>Ergebnis der Analyse einer einzelnen Spieldatei.</summary>
public sealed class GameFileInfo
{
    public required string Path { get; init; }
    public HubPlatform? Platform { get; set; }
    public string? GameCode { get; set; }
    public string? HeaderTitle { get; set; }
    public long Size { get; set; }
    /// <summary>Titel je Sprachcode aus den Metadaten (z. B. Wii-U-meta.xml).</summary>
    public Dictionary<string, string> LocalizedTitles { get; } = [];

    // Switch
    public SwitchContentKind SwitchKind { get; set; }
    public string? TitleId { get; set; }
    public string? BaseTitleId { get; set; }
    public int? Version { get; set; }
    public List<string> NcaFiles { get; } = [];
    public List<string> TicketTitleIds { get; } = [];
}

/// <summary>Updates und DLCs, die zu einem Switch-Basisspiel gehören (eigene Dumps).</summary>
public sealed class SwitchAddOns
{
    public List<GameFileInfo> Updates { get; } = [];
    public List<GameFileInfo> Dlcs { get; } = [];
}
