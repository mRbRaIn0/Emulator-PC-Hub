using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Emulation.Switch2;

/// <summary>
/// Eglantine (experimenteller Switch-2-Emulator, Fork von Eden). Start: <c>eglantine.exe -f -g &lt;Spiel.nup&gt;</c>.
/// Portabel über den Ordner <c>user</c> neben der EXE; Einstellungen liegen wie bei Eden in <c>user\config\qt-config.ini</c>.
/// Ein NUP-Container (Spiel + Update) startet ohne Keys und Firmware – die braucht nur Eglantine für verschlüsselte Dumps.
/// </summary>
public sealed class EglantineAdapter : AdapterBase
{
    public const string ExeName = "eglantine.exe";

    public EglantineAdapter(AppPaths paths, ConfigService config) : base(paths, config) { }

    public override string Id => EmulatorIds.Eglantine;
    public override string DisplayName => "Eglantine (Switch 2)";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.Switch2];

    private string? FindExe() => FindExecutable(ExeName, Config.Current.Emulators.Eglantine, Paths.IntegrationDir(Id));

    public static string DataDirectory(string exe) => Path.Combine(Path.GetDirectoryName(exe)!, "user");

    public string? ConfigFile() =>
        FindExe() is { } exe ? Path.Combine(DataDirectory(exe), "config", "qt-config.ini") : null;

    public override EmulatorInstallation DetectInstallation()
    {
        var exe = FindExe();
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

    public override IReadOnlyList<string> DetectGames() => [];

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = DetectInstallation();
        if (!install.IsInstalled)
            throw new LaunchException("Eglantine ist nicht installiert. Bitte unter Komponenten installieren (eigenes Paket „Aus Datei“).");
        if (!File.Exists(request.Game.Path))
            throw new LaunchException($"Spieldatei nicht gefunden: {request.Game.Path}");
        var exe = install.ExecutablePath!;
        DisableUpdateCheck(exe);
        var args = new List<string>();
        if (request.Fullscreen)
            args.Add("-f");
        if (!string.IsNullOrWhiteSpace(request.Preset?.Arguments))
            args.AddRange(CommandLine.Split(request.Preset.Arguments));
        args.Add("-g");
        args.Add(request.Game.Path);
        return new LaunchSpec
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(exe),
            Description = $"Eglantine: {request.Game.Title}",
        };
    }

    /// <summary>
    /// Eglantine fragt beim Start GitHub nach neuen Versionen und hat einen eigenen Updater. Der Hub lädt nichts selbst –
    /// deshalb wird die Abfrage abgeschaltet; Updates installierst du über Komponenten („Aus Datei“).
    /// </summary>
    private void DisableUpdateCheck(string exe)
    {
        try
        {
            var file = Path.Combine(DataDirectory(exe), "config", "qt-config.ini");
            if (!File.Exists(file))
                return; // erster Start: Eglantine legt die Datei an, beim nächsten Start wird sie angepasst
            var ini = EdenIni.Load(file);
            if (ini.Get("UI", "check_for_updates") == "false")
                return;
            ini.Set("UI", "check_for_updates", false);
            ini.Save(file);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Eglantine: Update-Abfrage konnte nicht abgeschaltet werden", ex);
        }
    }

    public override IReadOnlyDictionary<string, string> GetConfig()
    {
        if (ConfigFile() is not { } file)
            return new Dictionary<string, string>();
        var ini = EdenIni.Load(file);
        var result = new Dictionary<string, string>();
        foreach (var (section, key) in new[] { ("Renderer", "backend"), ("Renderer", "resolution_setup"), ("Renderer", "use_vsync"),
                     ("UI", "fullscreen") })
            if (ini.Get(section, key) is { } v)
                result[$"{section}/{key}"] = v;
        return result;
    }
}
