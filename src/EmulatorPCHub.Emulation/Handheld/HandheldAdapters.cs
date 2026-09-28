using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Handheld;

/// <summary>
/// Gemeinsame Basis für portable Handheld-Emulatoren, die mit <c>&lt;exe&gt; [-f] &lt;Spiel&gt;</c> starten.
/// Einstellungen (Tasten, Controller) bleiben im Emulator selbst – beide übernehmen Tastatur und SDL-Controller direkt.
/// </summary>
public abstract class HandheldAdapterBase : AdapterBase
{
    protected HandheldAdapterBase(AppPaths paths, ConfigService config) : base(paths, config) { }

    protected abstract string ExecutableName { get; }

    public override EmulatorInstallation DetectInstallation()
    {
        var exe = FindExecutable(ExecutableName,
            Config.Current.Emulators.Get(Id),
            Paths.IntegrationDir(Id));
        if (exe == null)
            return EmulatorInstallation.NotFound;
        return new EmulatorInstallation
        {
            ExecutablePath = exe,
            Version = FileVersion(exe),
            UserDataDir = DataDirectory(exe),
            Portable = true,
        };
    }

    /// <summary>Ordner mit Konfiguration/Spielständen des Emulators.</summary>
    public abstract string DataDirectory(string exe);

    public override IReadOnlyList<string> DetectGames() => [];

    /// <summary>Konfigurationsdatei des Emulators (null, wenn nicht installiert).</summary>
    public string? ConfigFile() => DetectInstallation().ExecutablePath is { } exe ? ConfigFile(exe) : null;

    protected abstract string ConfigFile(string exe);

    /// <summary>Wird vor jedem Start aufgerufen (z. B. Spielstand-Ordner setzen).</summary>
    protected virtual void BeforeLaunch(string exe, GameEntry game) { }

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = DetectInstallation();
        if (!install.IsInstalled)
            throw new LaunchException($"{DisplayName} ist nicht installiert. Bitte unter Komponenten installieren (eigenes Paket „Aus Datei“).");
        if (!File.Exists(request.Game.Path))
            throw new LaunchException($"Spieldatei nicht gefunden: {request.Game.Path}");
        var args = new List<string>();
        if (request.Fullscreen)
            args.Add("-f");
        if (!string.IsNullOrWhiteSpace(request.Preset?.Arguments))
            args.AddRange(CommandLine.Split(request.Preset.Arguments));
        args.Add(request.Game.Path);
        BeforeLaunch(install.ExecutablePath!, request.Game);
        return new LaunchSpec
        {
            FileName = install.ExecutablePath!,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath),
            Description = $"{DisplayName}: {request.Game.Title}",
        };
    }
}

/// <summary>melonDS für Nintendo DS. Portabel: melonDS.toml liegt neben der EXE; eigene freie BIOS, keine Dumps nötig.</summary>
public sealed class MelonDsAdapter : HandheldAdapterBase
{
    public MelonDsAdapter(AppPaths paths, ConfigService config) : base(paths, config) { }

    public override string Id => EmulatorIds.MelonDS;
    public override string DisplayName => "melonDS";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.DS];
    protected override string ExecutableName => "melonDS.exe";

    public override string DataDirectory(string exe) => Path.GetDirectoryName(exe)!;

    protected override string ConfigFile(string exe) => Path.Combine(DataDirectory(exe), "melonDS.toml");

    /// <summary>
    /// Eigener Spielstand-Ordner je Spiel (<c>saves\&lt;Spielcode&gt;</c>) – melonDS legt dort <c>&lt;ROM-Name&gt;.sav</c> ab.
    /// So lassen sich Spielstände im Hub pro Spiel sichern; ohne Hub landen sie wie gewohnt neben der ROM.
    /// </summary>
    public string? SaveDirectory(GameEntry game) =>
        DetectInstallation().ExecutablePath is { } exe ? SaveDirectory(exe, game) : null;

    private string SaveDirectory(string exe, GameEntry game) =>
        Path.Combine(DataDirectory(exe), "saves", SaveKey(game));

    private static string SaveKey(GameEntry game)
    {
        var key = !string.IsNullOrWhiteSpace(game.GameCode) ? game.GameCode! : Path.GetFileNameWithoutExtension(game.Path);
        return string.Concat(key.Split(Path.GetInvalidFileNameChars()));
    }

    protected override void BeforeLaunch(string exe, GameEntry game)
    {
        var dir = SaveDirectory(exe, game);
        Directory.CreateDirectory(dir);
        // Vorhandenen Spielstand neben der ROM (z. B. von früher) einmalig übernehmen
        var old = Path.ChangeExtension(game.Path, ".sav");
        var target = Path.Combine(dir, Path.GetFileName(old));
        if (File.Exists(old) && !File.Exists(target))
            File.Copy(old, target);
        var file = ConfigFile(exe);
        var toml = MelonToml.Load(file);
        toml.Set("Instance0", "SaveFilePath", dir);
        toml.Save(file);
    }
}

/// <summary>Azahar (Nachfolger von Citra) für Nintendo 3DS. Portabel über den Ordner „user“ neben azahar.exe.</summary>
public sealed class AzaharAdapter : HandheldAdapterBase
{
    public AzaharAdapter(AppPaths paths, ConfigService config) : base(paths, config) { }

    public override string Id => EmulatorIds.Azahar;
    public override string DisplayName => "Azahar";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.ThreeDS];
    protected override string ExecutableName => "azahar.exe";

    public override string DataDirectory(string exe) => Path.Combine(Path.GetDirectoryName(exe)!, "user");

    protected override string ConfigFile(string exe) => Path.Combine(DataDirectory(exe), "config", "qt-config.ini");

    /// <summary>Spielstand eines 3DS-Titels (Title-ID, 16 Hex-Zeichen) auf der emulierten SD-Karte.</summary>
    public string? SaveDirectory(string titleId)
    {
        if (DetectInstallation().ExecutablePath is not { } exe || titleId.Length != 16)
            return null;
        var zero = new string('0', 32);
        return Path.Combine(DataDirectory(exe), "sdmc", "Nintendo 3DS", zero, zero, "title",
            titleId[..8].ToLowerInvariant(), titleId[8..].ToLowerInvariant(), "data");
    }
}
