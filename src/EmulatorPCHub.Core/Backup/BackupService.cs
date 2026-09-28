using System.Text.Json;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Core.Backup;

public enum BackupCategory
{
    Saves,
    Mods,
    Config,
    Presets,
}

public sealed record BackupEntry(
    string Id,
    BackupCategory Category,
    string Label,
    DateTimeOffset CreatedAt,
    string BackupPath,
    string OriginalPath,
    bool IsDirectory,
    long SizeBytes);

/// <summary>
/// Automatische Backups vor Änderungen an Mods, Saves, Konfigurationen und Presets
/// (Plan Abschnitt 27). Struktur: <c>Backups/&lt;Kategorie&gt;/&lt;Zeitstempel&gt;_&lt;Name&gt;/</c>.
/// </summary>
public sealed class BackupService
{
    private const string ManifestName = "backup.json";
    private readonly string _root;

    /// <summary>Verzeichnisse über dieser Größe werden nicht kopiert (z. B. mehrere GB Mod-Daten).</summary>
    public long MaxDirectoryBytes { get; set; } = 512L * 1024 * 1024;

    public BackupService(string backupRoot)
    {
        _root = backupRoot;
    }

    public string Root => _root;

    public BackupEntry? BackupFile(BackupCategory category, string file, string? label = null)
    {
        if (!File.Exists(file))
            return null;
        try
        {
            var target = CreateSlot(category, label ?? Path.GetFileName(file));
            var dest = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
            return WriteManifest(target, category, label ?? Path.GetFileName(file), file, false, new FileInfo(dest).Length);
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Backup von {file} fehlgeschlagen", ex);
            return null;
        }
    }

    public BackupEntry? BackupDirectory(BackupCategory category, string directory, string? label = null)
    {
        if (!Directory.Exists(directory))
            return null;
        try
        {
            var size = DirectorySize(directory);
            if (size > MaxDirectoryBytes)
            {
                HubLog.Warn($"Backup übersprungen, Ordner zu groß ({size / 1024 / 1024} MB): {directory}");
                return null;
            }
            var target = CreateSlot(category, label ?? Path.GetFileName(directory));
            var dest = Path.Combine(target, "content");
            CopyDirectory(directory, dest);
            return WriteManifest(target, category, label ?? Path.GetFileName(directory), directory, true, size);
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Backup von {directory} fehlgeschlagen", ex);
            return null;
        }
    }

    /// <summary>Sichert einen kleinen Text-Zustand (z. B. Mod-Aktivierungen) als JSON.</summary>
    public BackupEntry? BackupState(BackupCategory category, string label, object state)
    {
        try
        {
            var target = CreateSlot(category, label);
            var dest = Path.Combine(target, "state.json");
            File.WriteAllText(dest, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            return WriteManifest(target, category, label, "", false, new FileInfo(dest).Length);
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Zustands-Backup {label} fehlgeschlagen", ex);
            return null;
        }
    }

    public IReadOnlyList<BackupEntry> List(BackupCategory? category = null)
    {
        var result = new List<BackupEntry>();
        if (!Directory.Exists(_root))
            return result;
        foreach (var catDir in Directory.EnumerateDirectories(_root))
        {
            foreach (var slot in Directory.EnumerateDirectories(catDir))
            {
                var manifest = Path.Combine(slot, ManifestName);
                if (!File.Exists(manifest))
                    continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<BackupEntry>(File.ReadAllText(manifest));
                    if (entry != null && (category == null || entry.Category == category))
                        result.Add(entry);
                }
                catch (JsonException)
                {
                }
            }
        }
        return result.OrderByDescending(e => e.CreatedAt).ToList();
    }

    /// <summary>Stellt ein Backup wieder her. Der aktuelle Zustand wird vorher selbst gesichert.</summary>
    public bool Restore(BackupEntry entry)
    {
        try
        {
            if (string.IsNullOrEmpty(entry.OriginalPath))
                return false;
            if (entry.IsDirectory)
            {
                if (Directory.Exists(entry.OriginalPath))
                {
                    BackupDirectory(entry.Category, entry.OriginalPath, entry.Label + "-vor-wiederherstellung");
                    Directory.Delete(entry.OriginalPath, recursive: true);
                }
                CopyDirectory(Path.Combine(entry.BackupPath, "content"), entry.OriginalPath);
            }
            else
            {
                BackupFile(entry.Category, entry.OriginalPath, entry.Label + "-vor-wiederherstellung");
                Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
                File.Copy(Path.Combine(entry.BackupPath, Path.GetFileName(entry.OriginalPath)), entry.OriginalPath, overwrite: true);
            }
            HubLog.Info($"Backup wiederhergestellt: {entry.Label} → {entry.OriginalPath}");
            return true;
        }
        catch (Exception ex)
        {
            HubLog.Error($"Wiederherstellung von {entry.Label} fehlgeschlagen", ex);
            return false;
        }
    }

    /// <summary>Behält pro Kategorie nur die neuesten <paramref name="keep"/> Backups.</summary>
    public void Prune(int keep = 30)
    {
        foreach (var group in List().GroupBy(e => e.Category))
        {
            foreach (var old in group.Skip(keep))
            {
                try { Directory.Delete(old.BackupPath, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private string CreateSlot(BackupCategory category, string label)
    {
        var safe = string.Concat(label.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dir = Path.Combine(_root, category.ToString(), $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{safe}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static BackupEntry WriteManifest(string slot, BackupCategory category, string label, string original, bool isDir, long size)
    {
        var entry = new BackupEntry(Path.GetFileName(slot), category, label, DateTimeOffset.Now, slot, original, isDir, size);
        File.WriteAllText(Path.Combine(slot, ManifestName), JsonSerializer.Serialize(entry));
        return entry;
    }

    public static long DirectorySize(string dir)
    {
        long total = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(f).Length; }
            catch (IOException) { }
        }
        return total;
    }

    public static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dest, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(dest, Path.GetRelativePath(source, file)), overwrite: true);
    }
}
