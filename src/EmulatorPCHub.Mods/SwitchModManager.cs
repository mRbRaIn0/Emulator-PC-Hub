using System.IO.Compression;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Mods;

/// <summary>
/// Mod-Verwaltung für Switch-Spiele (Plan Abschnitt 13). Aktive Mods liegen im Mod-Ordner des Emulators,
/// deaktivierte im Mod-Speicher des Hubs (<c>integrations/switch/mods-store/&lt;TitleID&gt;/</c>).
/// Aktivieren/Deaktivieren verschiebt nur Ordner – ohne manuelles Kopieren. Liegen beide auf demselben
/// Laufwerk, geht das sofort, auch bei mehreren GB (z. B. CTGP Deluxe).
/// </summary>
public sealed class SwitchModManager
{
    private readonly AppPaths _paths;
    private readonly BackupService _backups;
    private readonly Func<SwitchEmulatorAdapter> _adapter;

    public SwitchModManager(AppPaths paths, BackupService backups, Func<SwitchEmulatorAdapter> adapter)
    {
        _paths = paths;
        _backups = backups;
        _adapter = adapter;
    }

    public string StoreDirectory(string titleId) =>
        Path.Combine(_paths.IntegrationDir("switch"), "mods-store", titleId.ToUpperInvariant());

    public string ActiveDirectory(string titleId) => _adapter().ModsDirectory(titleId);

    public IReadOnlyList<ModInfo> List(string titleId, string gameTitle = "")
    {
        var result = new List<ModInfo>();
        void Add(string dir, bool enabled)
        {
            if (!Directory.Exists(dir))
                return;
            foreach (var mod in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(mod);
                if (result.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                result.Add(new ModInfo
                {
                    Id = $"{titleId}/{name}",
                    Name = name,
                    GameKey = titleId,
                    GameTitle = gameTitle,
                    Enabled = enabled,
                    Path = mod,
                    Version = ReadVersion(mod),
                    Source = enabled ? "Emulator" : "Hub",
                });
            }
        }
        Add(ActiveDirectory(titleId), true);
        Add(StoreDirectory(titleId), false);
        return result.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string? ReadVersion(string modDir)
    {
        foreach (var f in new[] { "version.txt", "VERSION", "version" })
        {
            var p = Path.Combine(modDir, f);
            if (File.Exists(p))
                return File.ReadAllText(p).Trim();
        }
        return null;
    }

    public async Task SetEnabledAsync(ModInfo mod, bool enabled, IProgress<string>? progress = null)
    {
        if (mod.Enabled == enabled)
            return;
        BackupState(mod.GameKey, $"vor-{(enabled ? "aktivieren" : "deaktivieren")}-{mod.Name}");
        var target = enabled
            ? Path.Combine(ActiveDirectory(mod.GameKey), mod.Name)
            : Path.Combine(StoreDirectory(mod.GameKey), mod.Name);
        await MoveDirectoryAsync(mod.Path, target, progress);
        mod.Path = target;
        mod.Enabled = enabled;
        HubLog.Info($"Mod {(enabled ? "aktiviert" : "deaktiviert")}: {mod.Name} ({mod.GameKey})");
    }

    /// <summary>Aktiviert genau die angegebenen Mods (alle anderen werden deaktiviert).</summary>
    public async Task ActivateExactlyAsync(string titleId, IReadOnlyCollection<string> modNames, IProgress<string>? progress = null)
    {
        foreach (var mod in List(titleId))
        {
            var shouldBeEnabled = modNames.Contains(mod.Name, StringComparer.OrdinalIgnoreCase);
            if (mod.Enabled != shouldBeEnabled)
                await SetEnabledAsync(mod, shouldBeEnabled, progress);
        }
    }

    /// <summary>
    /// Installiert eine Mod aus ZIP oder Ordner in den Hub-Mod-Speicher. Unterstützt die üblichen Layouts:
    /// <c>atmosphere/contents/&lt;TID&gt;/…</c>, <c>&lt;Name&gt;/romfs|exefs</c> und <c>romfs|exefs</c> direkt.
    /// </summary>
    public async Task<ModInfo?> InstallAsync(string source, string titleId, string? name, IProgress<string>? progress = null)
    {
        var temp = Path.Combine(_paths.Cache, "mod-import-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            string root;
            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Entpacke …");
                await Task.Run(() => ZipFile.ExtractToDirectory(source, temp));
                root = temp;
            }
            else if (Directory.Exists(source))
            {
                root = source;
            }
            else
            {
                throw new FileNotFoundException("Mod-Quelle nicht gefunden", source);
            }

            var modRoot = FindModRoot(root, titleId);
            if (modRoot == null)
                throw new InvalidDataException("Keine romfs/exefs-Ordner in der Mod gefunden.");

            name ??= Path.GetFileNameWithoutExtension(source);
            var target = Path.Combine(StoreDirectory(titleId), name);
            if (Directory.Exists(target))
            {
                _backups.BackupState(BackupCategory.Mods, $"ersetzt-{name}", new { replaced = target, at = DateTimeOffset.Now });
                Directory.Delete(target, recursive: true);
            }
            progress?.Report("Kopiere Mod-Dateien …");
            if (root == temp)
                await MoveDirectoryAsync(modRoot, target, progress);
            else
                await Task.Run(() => BackupService.CopyDirectory(modRoot, target));
            HubLog.Info($"Mod installiert: {name} → {target}");
            return List(titleId).FirstOrDefault(m => m.Name == name);
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, recursive: true);
            }
            catch (IOException) { }
        }
    }

    private static string? FindModRoot(string root, string titleId)
    {
        var atmos = Path.Combine(root, "atmosphere", "contents", titleId);
        if (Directory.Exists(atmos))
            return atmos;
        var atmosLower = Path.Combine(root, "atmosphere", "contents", titleId.ToLowerInvariant());
        if (Directory.Exists(atmosLower))
            return atmosLower;
        if (Directory.Exists(Path.Combine(root, "romfs")) || Directory.Exists(Path.Combine(root, "exefs")))
            return root;
        foreach (var sub in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Directory.Exists(Path.Combine(sub, "romfs")) || Directory.Exists(Path.Combine(sub, "exefs")))
                return sub;
        }
        return null;
    }

    private void BackupState(string titleId, string label)
    {
        var state = List(titleId).Select(m => new { m.Name, m.Enabled, m.Version }).ToList();
        _backups.BackupState(BackupCategory.Mods, $"{titleId}-{label}", state);
    }

    /// <summary>Verschiebt einen Ordner; über Laufwerksgrenzen hinweg wird kopiert und danach gelöscht.</summary>
    public static async Task MoveDirectoryAsync(string source, string target, IProgress<string>? progress)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), Path.GetPathRoot(Path.GetFullPath(target)),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, target);
            return;
        }
        progress?.Report("Kopiere über Laufwerksgrenzen …");
        await Task.Run(() =>
        {
            BackupService.CopyDirectory(source, target);
            Directory.Delete(source, recursive: true);
        });
    }
}
