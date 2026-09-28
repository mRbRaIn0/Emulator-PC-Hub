using System.Xml.Linq;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Cemu;

/// <summary>Cemu für Wii U (Plan 5.3). Start: <c>Cemu.exe -g &lt;Spiel&gt; -f</c>.</summary>
public sealed class CemuAdapter : AdapterBase
{
    public CemuAdapter(AppPaths paths, ConfigService config) : base(paths, config) { }

    public override string Id => EmulatorIds.Cemu;
    public override string DisplayName => "Cemu";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.WiiU];

    public override EmulatorInstallation DetectInstallation()
    {
        var exe = FindExecutable("Cemu.exe",
            Config.Current.Emulators.Cemu,
            Paths.IntegrationDir("cemu"),
            Path.Combine(ProgramFiles, "Cemu"),
            Path.Combine(LocalAppData, "Programs", "Cemu"));
        if (exe == null)
            return EmulatorInstallation.NotFound;
        var dataDir = DataDirectory(exe);
        var install = new EmulatorInstallation
        {
            ExecutablePath = exe,
            Version = FileVersion(exe) ?? Path.GetFileName(Path.GetDirectoryName(exe))?.Replace("Cemu_", ""),
            UserDataDir = dataDir,
            Portable = Directory.Exists(Path.Combine(Path.GetDirectoryName(exe)!, "portable")),
        };
        if (!File.Exists(Path.Combine(MlcPath(exe), "sys", "title", "0005001b", "10056000", "content", "Common", "Package", "Mii", "MiiNameCheck.dat"))
            && !Directory.Exists(Path.Combine(MlcPath(exe), "sys", "title")))
            install.Notes.Add("Hinweis: Online-/Systemdateien (eigene Wii-U-Dumps) sind optional und nicht eingerichtet.");
        return install;
    }

    public string DataDirectory(string? exe = null)
    {
        exe ??= DetectInstallation().ExecutablePath;
        if (exe != null)
        {
            var portable = Path.Combine(Path.GetDirectoryName(exe)!, "portable");
            if (Directory.Exists(portable))
                return portable;
        }
        return Path.Combine(AppData, "Cemu");
    }

    public string SettingsFile(string? exe = null) => Path.Combine(DataDirectory(exe), "settings.xml");

    public string MlcPath(string? exe = null)
    {
        try
        {
            var file = SettingsFile(exe);
            if (File.Exists(file))
            {
                var mlc = XDocument.Load(file).Root?.Element("mlc_path")?.Value;
                if (!string.IsNullOrWhiteSpace(mlc))
                    return mlc;
            }
        }
        catch (Exception)
        {
        }
        return Path.Combine(DataDirectory(exe), "mlc01");
    }

    public string GraphicPacksDirectory(string? exe = null) => Path.Combine(DataDirectory(exe), "graphicPacks");

    public override IReadOnlyList<string> DetectGames()
    {
        try
        {
            var file = SettingsFile();
            if (!File.Exists(file))
                return [];
            return XDocument.Load(file).Root?.Element("GamePaths")?.Elements("Entry")
                .Select(e => e.Value).Where(Directory.Exists).ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = DetectInstallation();
        if (!install.IsInstalled)
            throw new LaunchException("Cemu ist nicht installiert. Bitte unter Komponenten installieren (eigenes Paket „Aus Datei“).");
        if (!File.Exists(request.Game.Path))
            throw new LaunchException($"Spieldatei nicht gefunden: {request.Game.Path}");
        var args = new List<string> { "-g", request.Game.Path };
        if (request.Fullscreen)
            args.Add("-f");
        if (!string.IsNullOrWhiteSpace(request.Preset?.Arguments))
            args.AddRange(CommandLine.Split(request.Preset.Arguments));

        // Skylanders: Portal emulieren und das Portal-Fenster automatisch mit öffnen
        Func<System.Diagnostics.Process, CancellationToken, Task>? afterStart = null;
        if (Skylanders.IsSkylandersGame(request.Game))
        {
            try { EnableSkylanderPortal(); }
            catch (Exception ex) { HubLog.Warn("Cemu: Skylanders-Portal konnte nicht aktiviert werden", ex); }
            var stateDir = Path.GetDirectoryName(SettingsFile())!;
            afterStart = (p, ct) => CemuUsbWindow.OpenAsync(p, stateDir, ct);
        }
        return new LaunchSpec
        {
            FileName = install.ExecutablePath!,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath),
            Description = $"Cemu: {request.Game.Title}",
            AfterStart = afterStart,
        };
    }

    /// <summary>settings.xml → EmulatedUsbDevices/EmulateSkylanderPortal = true.</summary>
    public void EnableSkylanderPortal()
    {
        var file = SettingsFile();
        if (!File.Exists(file))
            return;
        var doc = XDocument.Load(file);
        var usb = doc.Root!.Element("EmulatedUsbDevices");
        if (usb == null)
            doc.Root.Add(usb = new XElement("EmulatedUsbDevices"));
        if (usb.Element("EmulateSkylanderPortal")?.Value == "true")
            return;
        usb.SetElementValue("EmulateSkylanderPortal", "true");
        doc.Save(file);
    }

    /// <summary>Aktives Wii-U-Konto setzen (settings.xml → Account/PersistentId, dezimal) – trennt Spielstände pro Profil.</summary>
    public void SetActiveAccount(string persistentIdHex)
    {
        var file = SettingsFile();
        if (!File.Exists(file) || !uint.TryParse(persistentIdHex, System.Globalization.NumberStyles.HexNumber, null, out var id))
            return;
        var doc = XDocument.Load(file);
        var account = doc.Root!.Element("Account");
        if (account == null)
            doc.Root.Add(account = new XElement("Account"));
        var current = account.Element("PersistentId");
        if (current?.Value == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            return;
        account.SetElementValue("PersistentId", id);
        doc.Save(file);
    }

    /// <summary>Mii Maker (eigenes Wii-U-Systemtitel-Dump in der mlc01) – null, wenn nicht vorhanden.</summary>
    public string? MiiMakerExecutable()
    {
        foreach (var low in new[] { "1004a200", "1004a100", "1004a000" })
        {
            var code = Path.Combine(MlcPath(), "sys", "title", "00050010", low, "code");
            if (Directory.Exists(code) && Directory.EnumerateFiles(code, "*.rpx").FirstOrDefault() is { } rpx)
                return rpx;
        }
        return null;
    }

    /// <summary>Trägt einen Spieleordner in Cemu ein.</summary>
    public void AddGameFolder(string folder)
    {
        var file = SettingsFile();
        var doc = File.Exists(file) ? XDocument.Load(file) : new XDocument(new XElement("content"));
        var root = doc.Root!;
        var paths = root.Element("GamePaths");
        if (paths == null)
            root.Add(paths = new XElement("GamePaths"));
        if (paths.Elements("Entry").Any(e => string.Equals(e.Value, folder, StringComparison.OrdinalIgnoreCase)))
            return;
        paths.Add(new XElement("Entry", folder));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        doc.Save(file);
    }

    public override IReadOnlyDictionary<string, string> GetConfig()
    {
        var result = new Dictionary<string, string>();
        try
        {
            var file = SettingsFile();
            if (File.Exists(file))
            {
                var root = XDocument.Load(file).Root!;
                result["fullscreen"] = root.Element("fullscreen")?.Value ?? "false";
                result["mlc_path"] = root.Element("mlc_path")?.Value ?? "";
                result["Graphic.api"] = root.Element("Graphic")?.Element("api")?.Value ?? "";
            }
        }
        catch (Exception)
        {
        }
        return result;
    }
}
