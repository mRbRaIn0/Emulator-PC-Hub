using System.Text.RegularExpressions;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>
/// Kleine lokale Titel-Datenbank für bekannte Spiele (Anzeigenamen).
/// Unbekannte Spiele verwenden den Namen aus dem Disc-Header / meta.xml / Dateinamen.
/// </summary>
public static partial class TitleDatabase
{
    // Wii / GameCube: die ersten 3 Zeichen der Disc-ID (ohne Region)
    private static readonly Dictionary<string, string> DiscTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RMC"] = "Mario Kart Wii",
        ["RMG"] = "Super Mario Galaxy",
        ["SB4"] = "Super Mario Galaxy 2",
        ["RSP"] = "Wii Sports",
        ["RZT"] = "Wii Sports Resort",
        ["SF8"] = "Donkey Kong Country Returns",
        ["RZD"] = "The Legend of Zelda: Twilight Princess",
        ["SOU"] = "The Legend of Zelda: Skyward Sword",
        ["SUK"] = "Kirby's Adventure Wii",
        ["RSB"] = "Super Smash Bros. Brawl",
        ["SMN"] = "New Super Mario Bros. Wii",
        ["RMH"] = "Monster Hunter Tri",
        ["SX4"] = "Xenoblade Chronicles",
        ["GM4"] = "Mario Kart: Double Dash!!",
        ["GMS"] = "Super Mario Sunshine",
        ["GLM"] = "Luigi's Mansion",
        ["GZL"] = "The Legend of Zelda: The Wind Waker",
        ["GAL"] = "Super Smash Bros. Melee",
        ["GZ2"] = "The Legend of Zelda: Twilight Princess (GC)",
        ["GFZ"] = "F-Zero GX",
        ["GPV"] = "Pikmin",
        ["GPV2"] = "Pikmin 2",
        ["GM8"] = "Metroid Prime",
        ["G8M"] = "Paper Mario: Die Legende vom Äonentor",
    };

    private static readonly Dictionary<string, string> SwitchTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        [KnownGames.MarioKart8DeluxeTitleId] = "Mario Kart 8 Deluxe",
        ["0100000000010000"] = "Super Mario Odyssey",
        ["010015100B514000"] = "Super Mario Bros. Wonder",
        ["01007EF00011E000"] = "The Legend of Zelda: Breath of the Wild",
        ["0100F2C0115B6000"] = "The Legend of Zelda: Tears of the Kingdom",
        ["01006A800016E000"] = "Super Smash Bros. Ultimate",
        ["010028600EBDA000"] = "Super Mario 3D World + Bowser's Fury",
    };

    public static string? LookupDisc(string? gameCode)
    {
        if (string.IsNullOrEmpty(gameCode) || gameCode.Length < 3)
            return null;
        return DiscTitles.TryGetValue(gameCode[..3], out var t) ? t : null;
    }

    public static string? LookupSwitch(string? titleId) =>
        titleId != null && SwitchTitles.TryGetValue(titleId, out var t) ? t : null;

    [GeneratedRegex(@"[\[\(\{][^\]\)\}]*[\]\)\}]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Macht aus einem Dateinamen einen lesbaren Titel.</summary>
    public static string CleanFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.EndsWith(".nkit", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        name = Brackets().Replace(name, " ");
        name = name.Replace('_', ' ').Replace('.', ' ');
        name = Spaces().Replace(name, " ").Trim(' ', '-');
        return string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name;
    }

    /// <summary>Erkennt die Mario-Kart-Spezialseiten anhand von Code oder Titel.</summary>
    public static SpecialPage DetectSpecial(HubPlatform platform, string? code, string title)
    {
        if (platform == HubPlatform.Wii && KnownGames.IsMarioKartWiiCode(code))
            return SpecialPage.MarioKartWii;
        if (platform == HubPlatform.Wii && title.Contains("Mario Kart Wii", StringComparison.OrdinalIgnoreCase))
            return SpecialPage.MarioKartWii;
        if (platform == HubPlatform.Switch &&
            (KnownGames.IsMarioKart8DeluxeCode(code) || title.Contains("Mario Kart 8 Deluxe", StringComparison.OrdinalIgnoreCase)))
            return SpecialPage.MarioKart8Deluxe;
        return SpecialPage.None;
    }
}
