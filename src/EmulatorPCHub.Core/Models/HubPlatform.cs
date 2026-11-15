namespace EmulatorPCHub.Core.Models;

/// <summary>Plattformen, die der Hub darstellt. GameCube und Wii teilen sich intern Dolphin.</summary>
public enum HubPlatform
{
    GameCube,
    Wii,
    WiiU,
    Switch,
    DS,
    ThreeDS,
    /// <summary>Im Hub eingebaute Spiele (z. B. Hub Kart).</summary>
    BuiltIn,
    /// <summary>Am Ende angehängt: die Plattform steht als Zahl in der Bibliotheksdatenbank.</summary>
    Switch2,
}

public static class HubPlatformInfo
{
    public static IReadOnlyList<HubPlatform> Emulated { get; } =
        [HubPlatform.GameCube, HubPlatform.Wii, HubPlatform.WiiU, HubPlatform.Switch, HubPlatform.Switch2, HubPlatform.DS, HubPlatform.ThreeDS];

    public static string DisplayName(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube => "Nintendo GameCube",
        HubPlatform.Wii => "Nintendo Wii",
        HubPlatform.WiiU => "Nintendo Wii U",
        HubPlatform.Switch => "Nintendo Switch",
        HubPlatform.Switch2 => "Nintendo Switch 2",
        HubPlatform.DS => "Nintendo DS",
        HubPlatform.ThreeDS => "Nintendo 3DS",
        HubPlatform.BuiltIn => "PC Hub",
        _ => p.ToString(),
    };

    public static string ShortName(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube => "GameCube",
        HubPlatform.Wii => "Wii",
        HubPlatform.WiiU => "Wii U",
        HubPlatform.Switch => "Switch",
        HubPlatform.Switch2 => "Switch 2",
        HubPlatform.DS => "DS",
        HubPlatform.ThreeDS => "3DS",
        HubPlatform.BuiltIn => "Hub",
        _ => p.ToString(),
    };

    /// <summary>Schlüssel wie in der Konfiguration (libraryPaths.gamecube …).</summary>
    public static string ConfigKey(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube => "gamecube",
        HubPlatform.Wii => "wii",
        HubPlatform.WiiU => "wiiu",
        HubPlatform.Switch => "switch",
        HubPlatform.Switch2 => "switch2",
        HubPlatform.DS => "ds",
        HubPlatform.ThreeDS => "3ds",
        _ => "builtin",
    };

    /// <summary>Akzentfarbe (ARGB-Hex) für Kacheln und Hintergründe.</summary>
    public static string AccentHex(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube => "#FF6A4BC4",
        HubPlatform.Wii => "#FF3FA9F5",
        HubPlatform.WiiU => "#FF1FA3B8",
        HubPlatform.Switch => "#FFE60012",
        HubPlatform.Switch2 => "#FFE6A100",
        HubPlatform.DS => "#FF8C8C8C",
        HubPlatform.ThreeDS => "#FFCE181E",
        HubPlatform.BuiltIn => "#FFFF8A00",
        _ => "#FF808080",
    };

    /// <summary>Standard-Adapter für die Plattform.</summary>
    public static string DefaultAdapterId(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube or HubPlatform.Wii => EmulatorIds.Dolphin,
        HubPlatform.WiiU => EmulatorIds.Cemu,
        HubPlatform.Switch => EmulatorIds.Switch,
        HubPlatform.Switch2 => EmulatorIds.Eglantine,
        HubPlatform.DS => EmulatorIds.MelonDS,
        HubPlatform.ThreeDS => EmulatorIds.Azahar,
        _ => EmulatorIds.BuiltIn,
    };

    public static IReadOnlyList<string> FileExtensions(this HubPlatform p) => p switch
    {
        HubPlatform.GameCube => [".iso", ".gcm", ".gcz", ".rvz", ".ciso", ".wia", ".dol", ".elf"],
        HubPlatform.Wii => [".iso", ".wbfs", ".rvz", ".gcz", ".ciso", ".wia", ".wad", ".dol", ".elf"],
        HubPlatform.WiiU => [".wux", ".wud", ".wua", ".rpx"],
        HubPlatform.Switch => [".nsp", ".xci", ".nca", ".nro"],
        // NUP = Switch-2-Container (Spiel + Update); Eglantine lädt ihn direkt
        HubPlatform.Switch2 => [".nup"],
        HubPlatform.DS => [".nds", ".srl", ".dsi"],
        // .cia startet Azahar nicht direkt (erst über „CIA installieren“), daher nicht in der Bibliothek
        HubPlatform.ThreeDS => [".3ds", ".cci", ".cxi", ".3dsx", ".z3ds", ".zcci", ".zcxi"],
        _ => [],
    };

    public static bool TryParseConfigKey(string key, out HubPlatform platform)
    {
        foreach (var p in Enum.GetValues<HubPlatform>())
        {
            if (string.Equals(p.ConfigKey(), key, StringComparison.OrdinalIgnoreCase))
            {
                platform = p;
                return true;
            }
        }
        platform = HubPlatform.BuiltIn;
        return false;
    }
}

/// <summary>Stabile IDs der Emulator-Adapter.</summary>
public static class EmulatorIds
{
    public const string Dolphin = "dolphin";
    public const string Cemu = "cemu";
    public const string Switch = "switch";
    public const string WiiCompiled = "wiicompiled";
    public const string MelonDS = "melonds";
    public const string Azahar = "azahar";
    public const string Eglantine = "eglantine";
    public const string BuiltIn = "builtin";
}

/// <summary>Stabile IDs externer Komponenten (Komponenten-Seite / Updates).</summary>
public static class ComponentIds
{
    public const string Hub = "hub";
    public const string Dolphin = EmulatorIds.Dolphin;
    public const string Cemu = EmulatorIds.Cemu;
    public const string Switch = EmulatorIds.Switch;
    public const string WiiCompiled = EmulatorIds.WiiCompiled;
    public const string MelonDS = EmulatorIds.MelonDS;
    public const string Azahar = EmulatorIds.Azahar;
    public const string Eglantine = EmulatorIds.Eglantine;
    public const string WheelWizard = "wheelwizard";
    public const string RetroRewind = "retrorewind";
    public const string CtgpDeluxe = "ctgpdx";
    public const string PadForge = "padforge";
}
