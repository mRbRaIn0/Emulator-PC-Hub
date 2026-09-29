using System.Text.Json.Serialization;

namespace EmulatorPCHub.Core.Config;

/// <summary>
/// Hub-Konfiguration (Plan Abschnitt 25). Wird als <c>data/config.json</c> gespeichert
/// und ist bewusst von Hand editierbar.
/// </summary>
public sealed class HubConfig
{
    [JsonPropertyName("libraryPaths")]
    public LibraryPathsConfig LibraryPaths { get; set; } = new();

    /// <summary>Zusätzliche Ordner (Plattform wird per Datei-Header erkannt).</summary>
    [JsonPropertyName("extraLibraryFolders")]
    public List<string> ExtraLibraryFolders { get; set; } = [];

    [JsonPropertyName("emulators")]
    public EmulatorPathsConfig Emulators { get; set; } = new();

    [JsonPropertyName("ui")]
    public UiConfig Ui { get; set; } = new();

    [JsonPropertyName("marioKartWii")]
    public MarioKartWiiConfig MarioKartWii { get; set; } = new();

    [JsonPropertyName("marioKart8Deluxe")]
    public MarioKart8DeluxeConfig MarioKart8Deluxe { get; set; } = new();

    [JsonPropertyName("launch")]
    public LaunchConfig Launch { get; set; } = new();

    [JsonPropertyName("hubKart")]
    public HubKartConfig HubKart { get; set; } = new();

    [JsonPropertyName("setupCompleted")]
    public bool SetupCompleted { get; set; }

    [JsonPropertyName("activeProfileId")]
    public string? ActiveProfileId { get; set; }
}

public sealed class LibraryPathsConfig
{
    [JsonPropertyName("gamecube")] public string GameCube { get; set; } = "";
    [JsonPropertyName("wii")] public string Wii { get; set; } = "";
    [JsonPropertyName("wiiu")] public string WiiU { get; set; } = "";
    [JsonPropertyName("switch")] public string Switch { get; set; } = "";
    [JsonPropertyName("ds")] public string DS { get; set; } = "";
    [JsonPropertyName("3ds")] public string ThreeDS { get; set; } = "";

    public string Get(Models.HubPlatform platform) => platform switch
    {
        Models.HubPlatform.GameCube => GameCube,
        Models.HubPlatform.Wii => Wii,
        Models.HubPlatform.WiiU => WiiU,
        Models.HubPlatform.Switch => Switch,
        Models.HubPlatform.DS => DS,
        Models.HubPlatform.ThreeDS => ThreeDS,
        _ => "",
    };

    public void Set(Models.HubPlatform platform, string path)
    {
        switch (platform)
        {
            case Models.HubPlatform.GameCube: GameCube = path; break;
            case Models.HubPlatform.Wii: Wii = path; break;
            case Models.HubPlatform.WiiU: WiiU = path; break;
            case Models.HubPlatform.Switch: Switch = path; break;
            case Models.HubPlatform.DS: DS = path; break;
            case Models.HubPlatform.ThreeDS: ThreeDS = path; break;
        }
    }
}

public sealed class EmulatorPathsConfig
{
    [JsonPropertyName("dolphin")] public string Dolphin { get; set; } = "";
    [JsonPropertyName("dolphinUserDir")] public string DolphinUserDir { get; set; } = "";
    [JsonPropertyName("cemu")] public string Cemu { get; set; } = "";
    [JsonPropertyName("switch")] public string Switch { get; set; } = "";
    /// <summary>Welcher Switch-Adapter aktiv ist ("eden" als Standard).</summary>
    [JsonPropertyName("switchAdapter")] public string SwitchAdapter { get; set; } = "eden";
    [JsonPropertyName("switchDataDir")] public string SwitchDataDir { get; set; } = "";
    [JsonPropertyName("wiicompiled")] public string WiiCompiled { get; set; } = "";
    [JsonPropertyName("wheelwizard")] public string WheelWizard { get; set; } = "";
    [JsonPropertyName("melonds")] public string MelonDS { get; set; } = "";
    [JsonPropertyName("azahar")] public string Azahar { get; set; } = "";

    public string Get(string id) => id switch
    {
        Models.EmulatorIds.Dolphin => Dolphin,
        Models.EmulatorIds.Cemu => Cemu,
        Models.EmulatorIds.Switch => Switch,
        Models.EmulatorIds.WiiCompiled => WiiCompiled,
        Models.ComponentIds.WheelWizard => WheelWizard,
        Models.EmulatorIds.MelonDS => MelonDS,
        Models.EmulatorIds.Azahar => Azahar,
        _ => "",
    };

    public void Set(string id, string path)
    {
        switch (id)
        {
            case Models.EmulatorIds.Dolphin: Dolphin = path; break;
            case Models.EmulatorIds.Cemu: Cemu = path; break;
            case Models.EmulatorIds.Switch: Switch = path; break;
            case Models.EmulatorIds.WiiCompiled: WiiCompiled = path; break;
            case Models.ComponentIds.WheelWizard: WheelWizard = path; break;
            case Models.EmulatorIds.MelonDS: MelonDS = path; break;
            case Models.EmulatorIds.Azahar: Azahar = path; break;
        }
    }
}

public sealed class UiConfig
{
    /// <summary>"console" (randloses Vollbild) oder "desktop".</summary>
    [JsonPropertyName("startupMode")] public string StartupMode { get; set; } = "desktop";
    /// <summary>"switch" (dunkel) oder "switch-light".</summary>
    [JsonPropertyName("theme")] public string Theme { get; set; } = "switch";
    [JsonPropertyName("animations")] public bool Animations { get; set; } = true;
    [JsonPropertyName("sounds")] public bool Sounds { get; set; } = true;
    [JsonPropertyName("soundVolume")] public double SoundVolume { get; set; } = 0.6;
    [JsonPropertyName("vibration")] public bool Vibration { get; set; } = true;
    /// <summary>A/B tauschen (Nintendo-Layout bei XInput-Controllern).</summary>
    [JsonPropertyName("swapConfirmButtons")] public bool SwapConfirmButtons { get; set; }
    [JsonPropertyName("autostart")] public bool Autostart { get; set; }
    [JsonPropertyName("showSpecialTiles")] public bool ShowSpecialTiles { get; set; } = true;
    [JsonPropertyName("clock24h")] public bool Clock24h { get; set; } = true;
    /// <summary>Erstsprache (Oberfläche, Spieltitel, Cover), z. B. "de" oder "en".</summary>
    [JsonPropertyName("primaryLanguage")] public string PrimaryLanguage { get; set; } = "de";
    /// <summary>Zweitsprache – wird genutzt, wenn ein Spiel keine Titel/Cover in der Erstsprache hat.</summary>
    [JsonPropertyName("secondaryLanguage")] public string SecondaryLanguage { get; set; } = "en";
}

public sealed class MarioKartWiiConfig
{
    /// <summary>Eigener Dump (RMCP01 für WiiCompiled). Leer = automatisch (Bibliothek / Wheel Wizard).</summary>
    [JsonPropertyName("gameFile")] public string GameFile { get; set; } = "";
    /// <summary>"wiicompiled" oder "dolphin".</summary>
    [JsonPropertyName("engine")] public string Engine { get; set; } = "wiicompiled";
    [JsonPropertyName("preset")] public string Preset { get; set; } = "mkwii-vanilla";
    [JsonPropertyName("retroRewindMyStuff")] public bool RetroRewindMyStuff { get; set; }
    [JsonPropertyName("retroRewindSeparateSave")] public bool RetroRewindSeparateSave { get; set; }
}

public sealed class MarioKart8DeluxeConfig
{
    [JsonPropertyName("gameFile")] public string GameFile { get; set; } = "";
    [JsonPropertyName("preset")] public string Preset { get; set; } = "mk8dx-vanilla";
    [JsonPropertyName("customMods")] public List<string> CustomMods { get; set; } = [];
}

public sealed class LaunchConfig
{
    /// <summary>Hub während des Spiels ausblenden (sonst minimieren).</summary>
    [JsonPropertyName("hideHubWhilePlaying")] public bool HideHubWhilePlaying { get; set; } = true;
    [JsonPropertyName("emulatorFullscreen")] public bool EmulatorFullscreen { get; set; } = true;
    /// <summary>Home + Minus 1,5 s halten beendet das laufende Spiel.</summary>
    [JsonPropertyName("homeComboStopsGame")] public bool HomeComboStopsGame { get; set; } = true;
    [JsonPropertyName("backupBeforeLaunch")] public bool BackupBeforeLaunch { get; set; } = true;
    /// <summary>Spielstände vor und nach jedem Spiel automatisch sichern (nur bei Änderungen, max. 10 pro Spiel/Profil).</summary>
    [JsonPropertyName("autoSaveSnapshots")] public bool AutoSaveSnapshots { get; set; } = true;
}

public sealed class HubKartConfig
{
    [JsonPropertyName("renderWidth")] public int RenderWidth { get; set; } = 960;
    [JsonPropertyName("renderHeight")] public int RenderHeight { get; set; } = 540;
    [JsonPropertyName("music")] public bool Music { get; set; } = true;
    [JsonPropertyName("musicVolume")] public double MusicVolume { get; set; } = 0.5;
    [JsonPropertyName("sfxVolume")] public double SfxVolume { get; set; } = 0.8;
    [JsonPropertyName("lastDriver")] public int LastDriver { get; set; }
    [JsonPropertyName("lastCc")] public int LastCc { get; set; } = 100;
    [JsonPropertyName("bestTimes")] public Dictionary<string, double> BestTimes { get; set; } = [];
    [JsonPropertyName("trophies")] public Dictionary<string, int> Trophies { get; set; } = [];
}
