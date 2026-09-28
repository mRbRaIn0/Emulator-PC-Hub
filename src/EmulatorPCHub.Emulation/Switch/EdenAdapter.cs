using System.Text.Json;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Emulation.Switch;

/// <summary>
/// Eden (Nachfolger von yuzu/Sudachi, git.eden-emu.dev). Start: <c>eden.exe -f -g &lt;Spiel&gt;</c>.
/// Daten: portabler Ordner <c>user\</c> neben eden.exe oder <c>%APPDATA%\eden</c>.
/// Keys: <c>user\keys\prod.keys</c>, Firmware: <c>user\nand\system\Contents\registered</c>,
/// Mods: <c>user\load\&lt;TitleID&gt;\&lt;Mod&gt;</c>, Einstellungen: <c>user\config\qt-config.ini</c>.
/// Eigene Updates/DLCs werden über Edens „externe Inhalte“-Ordner eingebunden – ohne Installation in die NAND.
/// </summary>
public sealed class EdenAdapter : SwitchEmulatorAdapter
{
    public const string ExeName = "eden.exe";
    private const string UiSection = "UI";
    private const string GameDirs = @"Paths\gamedirs";
    private const string ExternalDirs = @"Paths\external_content_dirs";

    private readonly BackupService? _backups;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public EdenAdapter(AppPaths paths, ConfigService config, BackupService? backups = null) : base(paths, config)
    {
        _backups = backups;
    }

    public override string DisplayName => "Eden (Switch)";
    public override string ImplementationId => "eden";

    private string? FindExe()
    {
        var configured = Config.Current.Emulators.Switch;
        if (!string.IsNullOrWhiteSpace(configured) && !Path.GetFileName(configured).Equals(ExeName, StringComparison.OrdinalIgnoreCase)
            && File.Exists(configured))
            configured = null; // Pfad eines anderen Emulators
        return FindExecutable(ExeName,
            configured,
            Paths.IntegrationDir("switch"),
            Path.Combine(LocalAppData, "Programs", "Eden"),
            Path.Combine(ProgramFiles, "Eden"));
    }

    public override EmulatorInstallation DetectInstallation()
    {
        var exe = FindExe();
        if (exe == null)
            return EmulatorInstallation.NotFound;
        var install = new EmulatorInstallation
        {
            ExecutablePath = exe,
            Version = HubVersion(exe) ?? FileVersion(exe),
            UserDataDir = DataDirectory(exe),
            Portable = Directory.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "user")),
        };
        var sys = GetSystemStatus();
        if (!sys.KeysPresent)
            install.Problems.Add($"prod.keys fehlt (eigene Keys aus deiner Switch nach {sys.KeysPath} kopieren).");
        if (!sys.FirmwarePresent)
            install.Problems.Add("Firmware nicht installiert (eigene Firmware über Eden → Tools → Install Firmware).");
        return install;
    }

    private static string? HubVersion(string exe)
    {
        var file = Path.Combine(Path.GetDirectoryName(exe)!, "hub-version.txt");
        return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }

    public override string DataDirectory(string? exe = null)
    {
        var configured = Config.Current.Emulators.SwitchDataDir;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        exe ??= FindExe();
        if (exe != null)
        {
            var portable = Path.Combine(Path.GetDirectoryName(exe)!, "user");
            if (Directory.Exists(portable))
                return portable;
        }
        return Path.Combine(AppData, "eden");
    }

    public string ConfigFile => Path.Combine(DataDirectory(), "config", "qt-config.ini");

    public override SwitchSystemStatus GetSystemStatus()
    {
        var data = DataDirectory();
        var keys = Path.Combine(data, "keys", "prod.keys");
        var fw = Path.Combine(data, "nand", "system", "Contents", "registered");
        var fwPresent = Directory.Exists(fw) && Directory.EnumerateFileSystemEntries(fw).Any();
        return new SwitchSystemStatus(File.Exists(keys), fwPresent, keys, fw);
    }

    public override IReadOnlyList<string> DetectGames() =>
        EdenIni.Load(ConfigFile).GetArray(UiSection, GameDirs, "path")
            .Where(p => p.Length > 0 && Directory.Exists(p)).ToList();

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = DetectInstallation();
        if (!install.IsInstalled)
            throw new LaunchException("Kein Switch-Emulator installiert (Eden). Bitte unter Komponenten installieren (eigenes Paket „Aus Datei“).");
        if (!File.Exists(request.Game.Path))
            throw new LaunchException($"Spieldatei nicht gefunden: {request.Game.Path}");
        var sys = GetSystemStatus();
        if (!sys.KeysPresent)
            throw new LaunchException($"Eden braucht deine eigenen Keys (prod.keys) unter {sys.KeysPath}.");
        var args = new List<string>();
        if (request.Fullscreen)
            args.Add("-f");
        if (!string.IsNullOrWhiteSpace(request.Preset?.Arguments))
            args.AddRange(CommandLine.Split(request.Preset.Arguments));
        args.Add("-g");
        args.Add(request.Game.Path);
        return new LaunchSpec
        {
            FileName = install.ExecutablePath!,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath),
            Description = $"Eden: {request.Game.Title}",
        };
    }

    public override string ModsDirectory(string titleId) =>
        Path.Combine(DataDirectory(), "load", titleId.ToUpperInvariant());

    // Welche Update-/DLC-Dateien der Hub eingebunden hat (Eden selbst merkt sich nur die Ordner).
    private string AddOnFile(string titleId) => Path.Combine(DataDirectory(), "hub", $"{titleId.ToUpperInvariant()}-addons.json");

    private sealed record AddOnState(string? Update, List<string> Dlcs);

    public override void RegisterAddOns(string titleId, IEnumerable<SwitchAddOnFile> updates, IEnumerable<SwitchAddOnFile> dlcs)
    {
        var updateList = updates.OrderBy(u => u.Version ?? 0).ToList();
        var dlcList = dlcs.ToList();
        var folders = updateList.Concat(dlcList)
            .Select(f => Path.GetDirectoryName(f.Path))
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
        if (folders.Count > 0)
            EditConfig(ini =>
            {
                var existing = ini.GetArray(UiSection, ExternalDirs, "path");
                var changed = false;
                foreach (var folder in folders)
                {
                    if (existing.Any(e => SamePath(e, folder)))
                        continue;
                    ini.AppendArray(UiSection, ExternalDirs, [("path", EdenIni.Quote(folder))]);
                    changed = true;
                }
                return changed;
            });

        var file = AddOnFile(titleId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var state = new AddOnState(updateList.Count > 0 ? updateList[^1].Path : null, dlcList.Select(d => d.Path).ToList());
        File.WriteAllText(file, JsonSerializer.Serialize(state, Indented));
        HubLog.Info($"Eden: {updateList.Count} Update(s), {dlcList.Count} DLC(s) für {titleId} über {folders.Count} Ordner eingebunden");
    }

    private AddOnState? ReadAddOns(string titleId)
    {
        var file = AddOnFile(titleId);
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<AddOnState>(File.ReadAllText(file)) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public override IReadOnlyList<string> RegisteredDlcPaths(string titleId) =>
        ReadAddOns(titleId)?.Dlcs.Where(File.Exists).ToList() ?? [];

    public override string? SelectedUpdatePath(string titleId) => ReadAddOns(titleId)?.Update;

    public override void AddGameFolder(string folder)
    {
        EditConfig(ini =>
        {
            var dirs = ini.GetArray(UiSection, GameDirs, "path");
            if (dirs.Any(d => SamePath(d, folder)))
                return false;
            if (dirs.Count == 0)
            {
                // Eden legt diese Einträge nur bei leerer Liste selbst an – sonst fehlten installierte Titel.
                foreach (var builtIn in new[] { "SDMC", "UserNAND", "SysNAND" })
                    ini.AppendArray(UiSection, GameDirs, GameDirFields(builtIn, deepScan: false));
            }
            ini.AppendArray(UiSection, GameDirs, GameDirFields(EdenIni.Quote(folder), deepScan: true));
            return true;
        });
    }

    /// <summary>Aktiven Eden-Benutzer setzen ([System] current_user = Index in profiles.dat).</summary>
    public void SetCurrentUser(int index) =>
        EditConfig(ini =>
        {
            if (ini.Get("System", "current_user") == index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && ini.Get("System", @"current_user\default") == "false")
                return false;
            ini.Set("System", "current_user", index);
            return true;
        });

    private static List<(string, string)> GameDirFields(string path, bool deepScan) =>
    [
        ("path", path),
        (@"deep_scan\default", deepScan ? "false" : "true"),
        ("deep_scan", deepScan ? "true" : "false"),
        (@"expanded\default", "true"),
        ("expanded", "true"),
    ];

    /// <summary>Ändert qt-config.ini (legt sie bei Bedarf an; Eden ergänzt fehlende Werte beim Start).</summary>
    public void EditConfig(Func<EdenIni, bool> edit)
    {
        var file = ConfigFile;
        var ini = EdenIni.Load(file);
        if (!edit(ini))
            return;
        if (File.Exists(file))
            _backups?.BackupFile(BackupCategory.Config, file, "eden-qt-config");
        ini.Save(file);
    }

    private static bool SamePath(string a, string b)
    {
        static string Norm(string p) => p.Replace('/', '\\').TrimEnd('\\');
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }

    public override IReadOnlyDictionary<string, string> GetConfig()
    {
        var ini = EdenIni.Load(ConfigFile);
        var result = new Dictionary<string, string>();
        foreach (var (section, key) in new[] { ("Renderer", "backend"), ("Renderer", "resolution_setup"), ("Renderer", "use_vsync"),
                     ("System", "use_docked_mode"), ("UI", "fullscreen") })
            if (ini.Get(section, key) is { } v)
                result[$"{section}/{key}"] = v;
        return result;
    }
}
