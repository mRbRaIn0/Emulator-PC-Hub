using System.Text.Json;
using System.Text.Json.Nodes;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.WheelWizard;

namespace EmulatorPCHub.Emulation.Dolphin;

/// <summary>
/// Dolphin für GameCube und Wii (Plan 5.1/5.2). Beide Plattformen teilen sich dieselbe Installation.
/// Start: <c>Dolphin.exe -b -e &lt;Spiel oder Mod-Descriptor&gt;</c> – Dolphin beendet sich mit dem Spiel.
/// </summary>
public sealed class DolphinAdapter : AdapterBase
{
    private readonly WheelWizardIntegration _wheelWizard;

    public DolphinAdapter(AppPaths paths, ConfigService config, WheelWizardIntegration wheelWizard) : base(paths, config)
    {
        _wheelWizard = wheelWizard;
    }

    public override string Id => EmulatorIds.Dolphin;
    public override string DisplayName => "Dolphin";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.GameCube, HubPlatform.Wii];

    public override EmulatorInstallation DetectInstallation()
    {
        var exe = FindExecutable("Dolphin.exe",
            Config.Current.Emulators.Dolphin,
            Paths.IntegrationDir("dolphin"),
            _wheelWizard.ReadConfig().DolphinLocation,
            Path.Combine(ProgramFiles, "Dolphin"),
            Path.Combine(ProgramFiles, "Dolphin-x64"),
            Path.Combine(LocalAppData, "Programs", "Dolphin"));
        if (exe == null)
            return EmulatorInstallation.NotFound;

        var userDir = UserDirectory(exe);
        var portable = File.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "portable.txt"));
        var install = new EmulatorInstallation
        {
            ExecutablePath = exe,
            Version = DolphinVersion(exe),
            UserDataDir = userDir,
            Portable = portable,
        };
        if (portable)
            install.Notes.Add("Portabler Modus (User-Ordner neben Dolphin.exe)");
        return install;
    }

    /// <summary>Ist der Mii-Kanal (eigener Wii-NAND-Dump) in Dolphin installiert?</summary>
    public bool HasMiiChannel() =>
        File.Exists(Path.Combine(UserDirectory(), "Wii", "title", "00010002", "48414341", "content", "title.tmd"));

    public string UserDirectory(string? exe = null)
    {
        var configured = Config.Current.Emulators.DolphinUserDir;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        exe ??= DetectInstallation().ExecutablePath;
        if (exe != null)
        {
            var dir = Path.GetDirectoryName(exe)!;
            if (File.Exists(Path.Combine(dir, "portable.txt")))
                return Path.Combine(dir, "User");
        }
        var appData = Path.Combine(AppData, "Dolphin Emulator");
        if (Directory.Exists(appData))
            return appData;
        var docs = Path.Combine(Documents, "Dolphin Emulator");
        return Directory.Exists(docs) ? docs : appData;
    }

    private static string? DolphinVersion(string exe)
    {
        var v = FileVersion(exe);
        if (v != null && !v.StartsWith("0.0"))
            return v;
        // Ordnername wie "dolphin-2606a-x64"
        var folder = Path.GetFileName(Path.GetDirectoryName(exe));
        return folder;
    }

    public override IReadOnlyList<string> DetectGames()
    {
        var ini = new IniFile(Path.Combine(UserDirectory(), "Config", "Dolphin.ini"));
        var result = new List<string>();
        if (!int.TryParse(ini.Get("General", "ISOPaths"), out var count))
            count = 0;
        for (int i = 0; i < Math.Max(count, 16); i++)
        {
            var p = ini.Get("General", $"ISOPath{i}");
            if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                result.Add(p);
        }
        return result;
    }

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = DetectInstallation();
        if (!install.IsInstalled)
            throw new LaunchException("Dolphin ist nicht installiert. Bitte unter Komponenten installieren (eigenes Paket „Aus Datei“).");
        var game = request.Game;
        if (string.IsNullOrEmpty(game.Path) || !File.Exists(game.Path))
            throw new LaunchException($"Spieldatei nicht gefunden: {game.Path}");

        var target = game.Path;
        var description = $"Dolphin: {game.Title}";
        if (request.Preset?.Kind == PresetKind.RetroRewind)
        {
            var rr = RetroRewindLayout.Find(install.UserDataDir!);
            if (rr == null)
                throw new LaunchException("Retro Rewind ist nicht installiert (Load/Riivolution/…/RetroRewind6 fehlt).");
            target = WriteModDescriptor(rr, game.Path, request.Preset.Name, Config.Current.MarioKartWii);
            description = $"Dolphin: {game.Title} + Retro Rewind {rr.Version}";
        }

        var args = new List<string> { "-b", "-e", target };
        if (!string.IsNullOrWhiteSpace(Config.Current.Emulators.DolphinUserDir))
        {
            args.Add("-u");
            args.Add(Config.Current.Emulators.DolphinUserDir);
        }
        if (request.Fullscreen)
        {
            args.Add("-C");
            args.Add("Dolphin.Display.Fullscreen=True");
        }
        args.Add("-C");
        args.Add("Dolphin.Interface.ConfirmStop=False");
        if (game.Platform == HubPlatform.Wii)
        {
            // PC-Tastatur als Wii-USB-Tastatur: Texteingaben in Spielen, die USB-Tastaturen unterstützen
            args.Add("-C");
            args.Add("Dolphin.Core.WiiKeyboard=True");
        }

        // Skylanders: Portal nur für diesen Start emulieren und das Portal-Fenster automatisch öffnen
        Func<System.Diagnostics.Process, CancellationToken, Task>? afterStart = null;
        if (Skylanders.IsSkylandersGame(game))
        {
            // Controller auch im Hintergrund, damit das Spiel reagiert, während man im Portal-Fenster klickt;
            // Warnungen aus (z. B. fehlendes SSL-Zertifikat der Wii-Onlinedienste), damit kein Pop-up das Spiel verdeckt.
            foreach (var setting in new[]
            {
                "Dolphin.EmulatedUSBDevices.EmulateSkylanderPortal=True",
                "Dolphin.Input.BackgroundInput=True",
                "Dolphin.Interface.UsePanicHandlers=False",
            })
            {
                args.Add("-C");
                args.Add(setting);
            }
            var stateDir = Path.Combine(UserDirectory(), "Config");
            afterStart = (p, ct) => DolphinPortalWindow.OpenAsync(p, stateDir, ct);
        }
        if (!string.IsNullOrWhiteSpace(request.Preset?.Arguments))
            args.AddRange(CommandLine.Split(request.Preset.Arguments));

        return new LaunchSpec
        {
            FileName = install.ExecutablePath!,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath),
            Description = description,
            AfterStart = afterStart,
        };
    }

    /// <summary>
    /// Schreibt einen Dolphin-„Game Mod Descriptor“ (dasselbe Format, das Wheel Wizard verwendet),
    /// damit Dolphin das Spiel direkt mit Retro Rewind (Riivolution) startet.
    /// </summary>
    private string WriteModDescriptor(RetroRewindLayout rr, string baseFile, string displayName, MarioKartWiiConfig mk)
    {
        var options = new JsonArray
        {
            Option(1, "Pack", rr.SectionName),
            Option(mk.RetroRewindMyStuff ? 1 : 0, "My Stuff", rr.SectionName),
        };
        if (mk.RetroRewindSeparateSave)
            options.Add(Option(1, "Seperate Savegame", rr.SectionName));

        var json = new JsonObject
        {
            ["base-file"] = Path.GetFullPath(baseFile),
            ["display-name"] = displayName,
            ["riivolution"] = new JsonObject
            {
                ["patches"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["options"] = options,
                        ["root"] = rr.RootFolder,
                        ["xml"] = rr.XmlFile,
                    },
                },
            },
            ["type"] = "dolphin-game-mod-descriptor",
            ["version"] = 1,
        };
        Directory.CreateDirectory(Paths.LaunchCache);
        var file = Path.Combine(Paths.LaunchCache, "retrorewind.json");
        File.WriteAllText(file, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return file;

        static JsonObject Option(int choice, string name, string section) => new()
        {
            ["choice"] = choice,
            ["option-name"] = name,
            ["section-name"] = section,
        };
    }

    public override IReadOnlyDictionary<string, string> GetConfig()
    {
        var ini = new IniFile(Path.Combine(UserDirectory(), "Config", "Dolphin.ini"));
        var gfx = new IniFile(Path.Combine(UserDirectory(), "Config", "GFX.ini"));
        return new Dictionary<string, string>
        {
            ["Display.Fullscreen"] = ini.Get("Display", "Fullscreen") ?? "False",
            ["Core.GFXBackend"] = ini.Get("Core", "GFXBackend") ?? "",
            ["Settings.InternalResolution"] = gfx.Get("Settings", "InternalResolution") ?? "1",
        };
    }

    public override void ApplyConfig(IReadOnlyDictionary<string, string> values)
    {
        var ini = new IniFile(Path.Combine(UserDirectory(), "Config", "Dolphin.ini"));
        var gfx = new IniFile(Path.Combine(UserDirectory(), "Config", "GFX.ini"));
        foreach (var (key, value) in values)
        {
            var parts = key.Split('.', 2);
            if (parts.Length != 2)
                continue;
            if (parts[0] == "Settings")
                gfx.Set(parts[0], parts[1], value);
            else
                ini.Set(parts[0], parts[1], value);
        }
        ini.Save();
        gfx.Save();
    }

    /// <summary>Trägt einen Spieleordner in Dolphins Liste ein (damit Dolphin ihn ebenfalls kennt).</summary>
    public void AddGameFolder(string folder)
    {
        var path = Path.Combine(UserDirectory(), "Config", "Dolphin.ini");
        var ini = new IniFile(path);
        var existing = DetectGames();
        if (existing.Any(e => e.Equals(folder, StringComparison.OrdinalIgnoreCase)))
            return;
        var count = existing.Count;
        ini.Set("General", $"ISOPath{count}", folder);
        ini.Set("General", "ISOPaths", (count + 1).ToString());
        ini.Save();
    }
}

/// <summary>Lage einer Retro-Rewind-Installation im Dolphin-Userordner.</summary>
public sealed record RetroRewindLayout(string RootFolder, string XmlFile, string DataFolder, string? Version, string SectionName)
{
    /// <summary>Sucht Retro Rewind im Wheel-Wizard-Layout und im klassischen Riivolution-Layout.</summary>
    public static RetroRewindLayout? Find(string dolphinUserDir)
    {
        foreach (var root in new[]
                 {
                     Path.Combine(dolphinUserDir, "Load", "Riivolution", "WheelWizard"),
                     Path.Combine(dolphinUserDir, "Load", "Riivolution"),
                 })
        {
            var xml = Path.Combine(root, "riivolution", "RetroRewind6.xml");
            var data = Path.Combine(root, "RetroRewind6");
            if (File.Exists(xml) && Directory.Exists(data))
            {
                var versionFile = Path.Combine(data, "version.txt");
                var version = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : null;
                return new RetroRewindLayout(root, xml, data, version, "Retro Rewind");
            }
        }
        return null;
    }

    public static string DefaultRoot(string dolphinUserDir) => Path.Combine(dolphinUserDir, "Load", "Riivolution", "WheelWizard");
}
