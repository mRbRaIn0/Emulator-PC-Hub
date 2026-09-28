using System.Diagnostics;
using System.IO.Compression;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Updates;

public sealed record ComponentDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    string Homepage,
    IReleaseSource? Source,
    bool HubInstallable);

public sealed record InstallProgress(string Text, double? Percent);

/// <summary>
/// Installiert/aktualisiert externe Komponenten portabel unter <c>integrations/</c> (Plan Abschnitte 15/16).
/// Der Hub lädt nichts herunter: Der Nutzer lädt das Paket selbst von der offiziellen Seite und wählt es aus.
/// Vor Updates wird die relevante Konfiguration gesichert.
/// </summary>
public sealed class ComponentInstaller
{
    private readonly AppPaths _paths;
    private readonly BackupService _backups;

    public ComponentInstaller(AppPaths paths, BackupService backups)
    {
        _paths = paths;
        _backups = backups;
    }

    public static IReadOnlyList<ComponentDefinition> Catalog { get; } =
    [
        new(ComponentIds.Dolphin, "Dolphin", "Emulator", "GameCube & Wii – auch Fallback für Mario Kart Wii mit Retro Rewind",
            "https://dolphin-emu.org", new DolphinReleaseSource(), true),
        new(ComponentIds.Cemu, "Cemu", "Emulator", "Wii U (Mario Kart 8, Zelda HD, …)",
            "https://cemu.info", new GitHubReleaseSource("cemu-project", "Cemu"), true),
        new(ComponentIds.Switch, "Eden", "Emulator", "Nintendo Switch (Mario Kart 8 Deluxe) – austauschbarer Switch-Adapter",
            "https://eden-emu.dev",
            new GitHubReleaseSource("eden-emu", "eden", "git.eden-emu.dev"), true),
        new(ComponentIds.MelonDS, "melonDS", "Emulator", "Nintendo DS – Tastatur & Controller, keine BIOS-Dumps nötig",
            "https://melonds.kuribo64.net", new GitHubReleaseSource("melonDS-emu", "melonDS"), true),
        new(ComponentIds.Azahar, "Azahar", "Emulator", "Nintendo 3DS (Nachfolger von Citra)",
            "https://azahar-emu.org", new GitHubReleaseSource("azahar-emu", "azahar"), true),
        new(ComponentIds.WiiCompiled, "WiiCompiled", "Engine", "Native PC-Version von Mario Kart Wii (braucht eigenen PAL-Dump)",
            "https://github.com/patchzyy/Wiicompiled", new GitHubReleaseSource("patchzyy", "Wiicompiled"), true),
        new(ComponentIds.WheelWizard, "Wheel Wizard", "Tool", "Mod-Verwaltung für Mario Kart Wii (Advanced Tools)",
            "https://github.com/TeamWheelWizard/WheelWizard",
            new GitHubReleaseSource("TeamWheelWizard", "WheelWizard"), true),
        new(ComponentIds.PadForge, "PadForge", "Controller", "Kompatibilitätsschicht: Remapping & virtuelle Xbox-Controller (optional)",
            "https://github.com/hifihedgehog/PadForge",
            new GitHubReleaseSource("hifihedgehog", "PadForge"), true),
        new(ComponentIds.RetroRewind, "Retro Rewind", "Mod", "Custom-Track-Distribution für Mario Kart Wii",
            "https://rwfc.net", new RetroRewindReleaseSource(), true),
        new(ComponentIds.CtgpDeluxe, "CTGP Deluxe", "Mod", "Custom Tracks & Cups für Mario Kart 8 Deluxe",
            "https://www.ctgpdx.com", null, true),
    ];

    public static ComponentDefinition? Find(string id) => Catalog.FirstOrDefault(c => c.Id == id);

    public string DownloadDir => Path.Combine(_paths.Integrations, "_downloads");

    /// <summary>Welche EXE ein gültiges Paket der Komponente enthalten muss.</summary>
    public static string? ExpectedExecutable(string componentId) => componentId switch
    {
        ComponentIds.Dolphin => "Dolphin.exe",
        ComponentIds.Cemu => "Cemu.exe",
        ComponentIds.Switch => "eden.exe",
        ComponentIds.MelonDS => "melonDS.exe",
        ComponentIds.Azahar => "azahar.exe",
        ComponentIds.PadForge => "PadForge.exe",
        ComponentIds.WheelWizard => "WheelWizard.exe",
        ComponentIds.WiiCompiled => "WiiCompiled-Setup.exe",
        _ => null,
    };

    /// <summary>
    /// Installiert eine vom Nutzer selbst heruntergeladene Datei (ZIP/7z bzw. EXE). Das Paket wird vorher geprüft.
    /// </summary>
    public Task<string> InstallFromFileAsync(string componentId, string file, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(file), @"(\d+\.\d+(\.\d+)?[a-z]?)");
        return InstallFileAsync(componentId, file, m.Success ? m.Groups[1].Value : "lokal", progress, ct);
    }

    private async Task<string> InstallFileAsync(string componentId, string file, string version, IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new InstallProgress("Prüfe Paket …", null));
        var isExe = file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        if (componentId is ComponentIds.WheelWizard or ComponentIds.WiiCompiled)
        {
            if (!isExe)
                throw new InvalidDataException($"Hier wird die EXE-Datei erwartet, nicht „{Path.GetFileName(file)}“.");
        }
        else if (isExe)
        {
            throw new InvalidDataException($"„{Path.GetFileName(file)}“ ist ein Programm – bitte das ZIP/7z-Archiv der Windows-Version verwenden.");
        }
        else
        {
            var expected = ExpectedExecutable(componentId)!;
            if (!await ArchiveContainsAsync(file, expected, ct))
                throw new InvalidDataException($"„{Path.GetFileName(file)}“ enthält keine {expected} – das ist nicht das richtige Paket " +
                                               "(z. B. Quellcode statt Windows-Version).");
        }

        progress?.Report(new InstallProgress("Sichere Konfiguration …", null));
        BackupConfiguration(componentId);

        progress?.Report(new InstallProgress("Installiere …", null));
        var target = _paths.IntegrationDir(componentId);
        Directory.CreateDirectory(target);
        string exe;
        switch (componentId)
        {
            case ComponentIds.Dolphin:
                exe = await ExtractValidatedAsync(file, target, "Dolphin.exe", ct);
                var portable = Path.Combine(Path.GetDirectoryName(exe)!, "portable.txt");
                if (!File.Exists(portable))
                    File.WriteAllText(portable, "");
                break;
            case ComponentIds.Cemu:
                exe = await InstallCemuAsync(file, target, ct);
                break;
            case ComponentIds.Switch:
                exe = await ExtractValidatedAsync(file, target, "eden.exe", ct);
                // Portabler Modus: Eden nutzt den Ordner „user“ neben eden.exe statt %APPDATA%\eden
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(exe)!, "user"));
                break;
            case ComponentIds.MelonDS:
                exe = await ExtractValidatedAsync(file, target, "melonDS.exe", ct);
                // Portabler Modus: melonDS.toml neben der EXE
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "portable.txt"), "");
                break;
            case ComponentIds.Azahar:
                exe = await ExtractValidatedAsync(file, target, "azahar.exe", ct);
                // Portabler Modus: Azahar nutzt den Ordner „user“ neben azahar.exe statt %APPDATA%\Azahar
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(exe)!, "user"));
                break;
            case ComponentIds.WheelWizard:
                exe = Path.Combine(target, "WheelWizard.exe");
                if (!Path.GetFullPath(file).Equals(Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, exe, overwrite: true);
                File.WriteAllText(Path.Combine(target, "portable-ww.txt"), "");
                break;
            case ComponentIds.PadForge:
                if (Process.GetProcessesByName("PadForge").Length > 0)
                    throw new InvalidOperationException("PadForge läuft gerade – bitte zuerst beenden (Infobereich).");
                exe = await ExtractValidatedAsync(file, target, "PadForge.exe", ct);
                break;
            case ComponentIds.WiiCompiled:
                exe = Path.Combine(target, "WiiCompiled-Setup.exe");
                if (!Path.GetFullPath(file).Equals(Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
                    File.Copy(file, exe, overwrite: true);
                break;
            default:
                throw new NotSupportedException($"{componentId} wird über seine eigene Seite installiert.");
        }
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(exe)!, "hub-version.txt"), version);
        HubLog.Info($"Komponente installiert: {componentId} {version} → {exe}");
        progress?.Report(new InstallProgress($"Installiert: {version}", 100));
        return exe;
    }

    /// <summary>Entpackt zuerst in einen Zwischenordner, prüft die EXE und übernimmt dann alles (Nutzerdaten bleiben erhalten).</summary>
    private async Task<string> ExtractValidatedAsync(string archive, string target, string exeName, CancellationToken ct)
    {
        var staging = Path.Combine(_paths.Integrations, "_staging", Guid.NewGuid().ToString("N")[..8]);
        try
        {
            await ExtractAsync(archive, staging, ct);
            var found = FindFile(staging, exeName) ?? throw new InvalidDataException($"{exeName} nicht im Archiv gefunden.");
            var relativeExe = Path.GetRelativePath(staging, found);
            foreach (var f in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(target, Path.GetRelativePath(staging, f));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(f, dest, overwrite: true);
            }
            return Path.Combine(target, relativeExe);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Prüft ohne Entpacken, ob ein Archiv eine bestimmte Datei enthält.</summary>
    public static async Task<bool> ArchiveContainsAsync(string archive, string fileName, CancellationToken ct = default)
    {
        try
        {
            if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = ZipFile.OpenRead(archive);
                return zip.Entries.Any(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            }
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "tar.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-tf");
            psi.ArgumentList.Add(archive);
            using var p = Process.Start(psi)!;
            var listing = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            return listing.Split('\n')
                .Select(l => l.Trim().Replace('\\', '/'))
                .Any(l => l.Equals(fileName, StringComparison.OrdinalIgnoreCase) || l.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public sealed record LocalPackage(string Path, bool Valid);

    /// <summary>Archive, die der Nutzer in integrations/&lt;Komponente&gt;/ oder in _downloads/ abgelegt hat.</summary>
    public async Task<IReadOnlyList<LocalPackage>> FindLocalPackagesAsync(string componentId, CancellationToken ct = default)
    {
        var expected = ExpectedExecutable(componentId);
        if (expected == null || componentId is ComponentIds.WheelWizard or ComponentIds.WiiCompiled)
            return [];
        var result = new List<LocalPackage>();
        foreach (var dir in new[] { _paths.IntegrationDir(componentId), DownloadDir }.Where(Directory.Exists))
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is not (".zip" or ".7z"))
                    continue;
                var valid = await ArchiveContainsAsync(f, expected, ct);
                // Im Download-Cache nur passende Archive, im Komponentenordner auch falsche (mit Warnung)
                if (valid || dir != DownloadDir)
                    result.Add(new LocalPackage(f, valid));
            }
        }
        return result;
    }

    private async Task<string> InstallCemuAsync(string zip, string target, CancellationToken ct)
    {
        var before = Directory.GetDirectories(target, "Cemu_*").ToList();
        await ExtractAsync(zip, target, ct);
        var newest = Directory.GetDirectories(target, "Cemu_*")
            .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d)).First();
        foreach (var old in before.Where(b => !b.Equals(newest, StringComparison.OrdinalIgnoreCase)))
        {
            var oldPortable = Path.Combine(old, "portable");
            var newPortable = Path.Combine(newest, "portable");
            if (Directory.Exists(oldPortable) && !Directory.Exists(newPortable))
                Directory.Move(oldPortable, newPortable);
            try { Directory.Delete(old, recursive: true); }
            catch (IOException ex) { HubLog.Warn($"Alte Cemu-Version konnte nicht entfernt werden: {old}", ex); }
        }
        Directory.CreateDirectory(Path.Combine(newest, "portable"));
        return Path.Combine(newest, "Cemu.exe");
    }

    private void BackupConfiguration(string componentId)
    {
        var dir = _paths.IntegrationDir(componentId);
        if (!Directory.Exists(dir))
            return;
        var candidates = componentId switch
        {
            ComponentIds.Dolphin => Directory.GetDirectories(dir, "Config", SearchOption.AllDirectories),
            ComponentIds.Cemu => Directory.GetFiles(dir, "settings.xml", SearchOption.AllDirectories),
            ComponentIds.Switch => Directory.Exists(Path.Combine(dir, "user", "config"))
                ? Directory.GetFiles(Path.Combine(dir, "user", "config"), "*.ini", SearchOption.AllDirectories)
                : [],
            ComponentIds.WheelWizard => Directory.GetFiles(dir, "config.json", SearchOption.AllDirectories),
            ComponentIds.PadForge => Directory.GetFiles(dir, "PadForge.xml", SearchOption.TopDirectoryOnly),
            ComponentIds.MelonDS => Directory.GetFiles(dir, "melonDS.toml", SearchOption.AllDirectories),
            ComponentIds.Azahar => Directory.GetFiles(dir, "qt-config.ini", SearchOption.AllDirectories),
            _ => [],
        };
        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
                _backups.BackupDirectory(BackupCategory.Config, c, $"{componentId}-config");
            else
                _backups.BackupFile(BackupCategory.Config, c, $"{componentId}-{Path.GetFileName(c)}");
        }
    }

    /// <summary>Entpackt ZIP (integriert) oder 7z (über das in Windows enthaltene tar.exe).</summary>
    public static async Task ExtractAsync(string archive, string target, CancellationToken ct)
    {
        Directory.CreateDirectory(target);
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(() => ZipFile.ExtractToDirectory(archive, target, overwriteFiles: true), ct);
            return;
        }
        var tar = Path.Combine(Environment.SystemDirectory, "tar.exe");
        var psi = new ProcessStartInfo(tar) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-xf");
        psi.ArgumentList.Add(archive);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(target);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("tar.exe konnte nicht gestartet werden.");
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidDataException($"Entpacken fehlgeschlagen: {err}");
    }

    private static string? FindFile(string root, string name) =>
        Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
}
