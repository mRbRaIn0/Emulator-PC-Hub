using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Cemu;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.UI.Services;

/// <summary>Ein Speicherort eines Spielstands. <see cref="Shared"/> = gilt für alle Profile (z. B. Dolphin).</summary>
public sealed record SaveLocation(string Key, string Path, string Description, bool Shared)
{
    public bool Exists => Directory.Exists(Path) && Directory.EnumerateFileSystemEntries(Path).Any();
}

public sealed class SaveManifest
{
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    [JsonPropertyName("platform")] public HubPlatform Platform { get; set; }
    [JsonPropertyName("profileId")] public string ProfileId { get; set; } = "";
    [JsonPropertyName("profileName")] public string ProfileName { get; set; } = "";
    [JsonPropertyName("created")] public DateTimeOffset Created { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("auto")] public bool Auto { get; set; }
    [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = "";
    [JsonPropertyName("locations")] public List<string> Locations { get; set; } = [];
}

/// <summary>Ein gesicherter Spielstand (ZIP mit <c>manifest.json</c> und einem Ordner pro Speicherort).</summary>
public sealed record SaveSnapshot(string File, SaveManifest Manifest)
{
    public long Size => new FileInfo(File).Length;
}

/// <summary>
/// Save Manager: Spielstände pro Spiel und Hub-Profil sichern, wiederherstellen, exportieren, importieren,
/// duplizieren sowie automatische Snapshots vor/nach dem Spielen. Getrennt von den Profilen selbst –
/// ein Profil bestimmt nur, welches Emulator-Konto (Cemu) bzw. welcher Benutzer (Eden) gemeint ist.
/// Dolphin kennt keine Benutzer: dort sind Spielstände für alle Profile gemeinsam.
/// </summary>
public sealed class SaveManager
{
    public const int KeepAutoSnapshots = 10;
    private const string ManifestName = "manifest.json";
    private static readonly JsonSerializerOptions Json = new(HubJson.Options) { WriteIndented = true };

    private readonly AppPaths _paths;
    private readonly AdapterRegistry _adapters;

    public SaveManager(AppPaths paths, AdapterRegistry adapters)
    {
        _paths = paths;
        _adapters = adapters;
    }

    public string Root => Path.Combine(_paths.Backups, "Saves");

    // ------------------------------------------------------------------
    // Speicherorte
    // ------------------------------------------------------------------

    public IReadOnlyList<SaveLocation> Locations(GameEntry game, UserProfile profile)
    {
        var code = game.GameCode;
        try
        {
            switch (game.Platform)
            {
                case HubPlatform.Wii when code is { Length: >= 4 }:
                {
                    var low = Convert.ToHexString(Encoding.ASCII.GetBytes(code[..4])).ToLowerInvariant();
                    var dir = Path.Combine(_adapters.Dolphin.UserDirectory(), "Wii", "title", "00010000", low, "data");
                    return [new SaveLocation("wii", dir, "Dolphin – Wii-Spielstand (für alle Profile)", true)];
                }
                case HubPlatform.GameCube:
                    return [new SaveLocation("gc", Path.Combine(_adapters.Dolphin.UserDirectory(), "GC"),
                        "Dolphin – GameCube-Memory-Cards (gemeinsam für alle GameCube-Spiele)", true)];
                case HubPlatform.WiiU when code is { Length: 16 }:
                {
                    var save = Path.Combine(_adapters.Cemu.MlcPath(), "usr", "save", code[..8].ToLowerInvariant(), code[8..].ToLowerInvariant(), "user");
                    var account = CemuAccountFor(profile);
                    return
                    [
                        new SaveLocation("user", Path.Combine(save, account), $"Cemu – Konto {account}", false),
                        new SaveLocation("common", Path.Combine(save, "common"), "Cemu – gemeinsame Daten (alle Konten)", true),
                    ];
                }
                case HubPlatform.DS:
                {
                    var dir = _adapters.MelonDS.SaveDirectory(game);
                    return dir == null ? [] : [new SaveLocation("ds", dir, "melonDS – Spielstand (für alle Profile)", true)];
                }
                case HubPlatform.ThreeDS when code is { Length: 16 }:
                {
                    var dir = _adapters.Azahar.SaveDirectory(code);
                    return dir == null ? [] : [new SaveLocation("3ds", dir, "Azahar – Spielstand auf der emulierten SD-Karte (für alle Profile)", true)];
                }
                case HubPlatform.Switch when code is { Length: 16 }:
                {
                    var data = _adapters.Switch.DataDirectory();
                    var user = EdenUserFor(profile);
                    if (user == null)
                        return [];
                    return [new SaveLocation("user", EdenProfiles.SaveDirectory(data, user.Uuid, code), $"Eden – Benutzer „{user.Name}“", false)];
                }
            }
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Spielstand-Ordner für {game.Title} nicht ermittelbar", ex);
        }
        return [];
    }

    /// <summary>Warum es für ein Spiel keine Speicherorte gibt (für die Anzeige).</summary>
    public static string? Unsupported(GameEntry game) => game.Platform switch
    {
        HubPlatform.BuiltIn => "Eingebautes Spiel ohne Spielstand-Ordner.",
        HubPlatform.Wii or HubPlatform.WiiU or HubPlatform.Switch or HubPlatform.ThreeDS when string.IsNullOrEmpty(game.GameCode) =>
            "Keine Spiel-ID erkannt – der Spielstand-Ordner lässt sich nicht bestimmen.",
        _ => null,
    };

    public string CemuAccountFor(UserProfile profile) =>
        string.IsNullOrWhiteSpace(profile.CemuAccount) ? "80000001" : profile.CemuAccount!;

    public EdenUser? EdenUserFor(UserProfile profile)
    {
        var users = EdenProfiles.List(_adapters.Switch.DataDirectory());
        return users.FirstOrDefault(u => u.Uuid.Equals(profile.EdenUser, StringComparison.OrdinalIgnoreCase)) ?? users.FirstOrDefault();
    }

    public bool HasSaves(GameEntry game, UserProfile profile) => Locations(game, profile).Any(l => l.Exists);

    // ------------------------------------------------------------------
    // Snapshots
    // ------------------------------------------------------------------

    private string Folder(GameEntry game, string profileId) => Path.Combine(Root, Safe(game.Id), Safe(profileId));

    private static string Safe(string s) =>
        string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));

    public IReadOnlyList<SaveSnapshot> Snapshots(GameEntry game, string? profileId = null)
    {
        var dir = Path.Combine(Root, Safe(game.Id));
        if (!Directory.Exists(dir))
            return [];
        var pattern = profileId == null ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var baseDir = profileId == null ? dir : Path.Combine(dir, Safe(profileId));
        if (!Directory.Exists(baseDir))
            return [];
        return Directory.EnumerateFiles(baseDir, "*.zip", pattern)
            .Select(ReadSnapshot)
            .Where(s => s != null)
            .Cast<SaveSnapshot>()
            .OrderByDescending(s => s.Manifest.Created)
            .ToList();
    }

    public static SaveSnapshot? ReadSnapshot(string zipFile)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipFile);
            var entry = zip.GetEntry(ManifestName);
            if (entry == null)
                return null;
            using var s = entry.Open();
            var manifest = JsonSerializer.Deserialize<SaveManifest>(s, Json);
            return manifest == null ? null : new SaveSnapshot(zipFile, manifest);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Sichert den aktuellen Spielstand. null, wenn es nichts zu sichern gibt (oder bei „auto“ nichts geändert wurde).</summary>
    public SaveSnapshot? CreateSnapshot(GameEntry game, UserProfile profile, string label, bool auto)
    {
        var locations = Locations(game, profile).Where(l => l.Exists).ToList();
        if (locations.Count == 0)
            return null;
        var fingerprint = Fingerprint(locations);
        if (auto && Snapshots(game, profile.Id).FirstOrDefault()?.Manifest.Fingerprint == fingerprint)
            return null; // unverändert seit dem letzten Snapshot

        var now = DateTimeOffset.Now;
        var dir = Folder(game, profile.Id);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (auto ? "_auto" : "") + ".zip");
        for (var i = 2; File.Exists(file); i++)
            file = Path.Combine(dir, now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + $"_{i}.zip");

        var manifest = new SaveManifest
        {
            GameId = game.Id,
            Game = game.Title,
            Platform = game.Platform,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            Created = now,
            Label = label,
            Auto = auto,
            Fingerprint = fingerprint,
            Locations = locations.Select(l => l.Key).ToList(),
        };
        var tmp = file + ".tmp";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var loc in locations)
            {
                foreach (var f in Directory.EnumerateFiles(loc.Path, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(loc.Path, f).Replace('\\', '/');
                    zip.CreateEntryFromFile(f, $"{loc.Key}/{rel}", CompressionLevel.Optimal);
                }
            }
            var m = zip.CreateEntry(ManifestName);
            using var s = m.Open();
            JsonSerializer.Serialize(s, manifest, Json);
        }
        File.Move(tmp, file);
        if (auto)
            PruneAuto(game, profile.Id);
        HubLog.Info($"Spielstand gesichert: {game.Title} ({profile.Name}) → {Path.GetFileName(file)}");
        return new SaveSnapshot(file, manifest);
    }

    private static string Fingerprint(IEnumerable<SaveLocation> locations)
    {
        var sb = new StringBuilder();
        foreach (var loc in locations.OrderBy(l => l.Key))
            foreach (var f in Directory.EnumerateFiles(loc.Path, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var info = new FileInfo(f);
                sb.Append(loc.Key).Append('/').Append(Path.GetRelativePath(loc.Path, f)).Append('|')
                  .Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
            }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    private void PruneAuto(GameEntry game, string profileId)
    {
        foreach (var old in Snapshots(game, profileId).Where(s => s.Manifest.Auto).Skip(KeepAutoSnapshots))
        {
            try { File.Delete(old.File); }
            catch (IOException ex) { HubLog.Warn("Alter Auto-Snapshot konnte nicht gelöscht werden", ex); }
        }
    }

    /// <summary>
    /// Stellt einen Snapshot für ein Profil wieder her. Der aktuelle Stand wird vorher automatisch gesichert.
    /// Das Spiel darf dabei nicht laufen.
    /// </summary>
    public void Restore(SaveSnapshot snapshot, GameEntry game, UserProfile profile)
    {
        CreateSnapshot(game, profile, "Vor Wiederherstellen", auto: true);
        var targets = Locations(game, profile).ToDictionary(l => l.Key);
        using var zip = ZipFile.OpenRead(snapshot.File);
        foreach (var key in snapshot.Manifest.Locations)
        {
            if (!targets.TryGetValue(key, out var target))
                throw new InvalidOperationException($"Speicherort „{key}“ gibt es für dieses Spiel/Profil nicht.");
            if (Directory.Exists(target.Path))
                Directory.Delete(target.Path, recursive: true);
            Directory.CreateDirectory(target.Path);
            var root = Path.GetFullPath(target.Path) + Path.DirectorySeparatorChar;
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(key + "/", StringComparison.Ordinal) && e.Name.Length > 0))
            {
                var dest = Path.GetFullPath(Path.Combine(target.Path, entry.FullName[(key.Length + 1)..]));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Ungültiger Pfad im Snapshot.");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
        HubLog.Info($"Spielstand wiederhergestellt: {game.Title} ({profile.Name}) ← {Path.GetFileName(snapshot.File)}");
    }

    /// <summary>Kopiert einen Snapshot in einen Ordner (Dateiname mit Spiel, Profil und Datum).</summary>
    public string Export(SaveSnapshot snapshot, string folder)
    {
        var m = snapshot.Manifest;
        var name = Safe($"{m.Game} - {m.ProfileName} - {m.Created:yyyy-MM-dd HH-mm}.zip");
        var target = Path.Combine(folder, name);
        File.Copy(snapshot.File, target, overwrite: true);
        return target;
    }

    /// <summary>Übernimmt eine exportierte Sicherung als Snapshot eines Profils (danach wiederherstellbar).</summary>
    public SaveSnapshot Import(string zipFile, GameEntry game, UserProfile profile)
    {
        var source = ReadSnapshot(zipFile) ?? throw new InvalidDataException("Die Datei ist keine Spielstand-Sicherung des Hubs.");
        if (source.Manifest.Platform != game.Platform)
            throw new InvalidDataException($"Die Sicherung ist für {source.Manifest.Platform.DisplayName()}, nicht für {game.Platform.DisplayName()}.");
        return CopyTo(source, game, profile, "Import: " + source.Manifest.Game);
    }

    /// <summary>Dupliziert einen Snapshot in ein anderes Profil (z. B. Spielstand für „Spieler 2“ übernehmen).</summary>
    public SaveSnapshot Duplicate(SaveSnapshot snapshot, GameEntry game, UserProfile target) =>
        CopyTo(snapshot, game, target, $"Kopie von {snapshot.Manifest.ProfileName}");

    private SaveSnapshot CopyTo(SaveSnapshot source, GameEntry game, UserProfile profile, string label)
    {
        var dir = Folder(game, profile.Id);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "_copy.zip");
        for (var i = 2; File.Exists(file); i++)
            file = Path.Combine(dir, DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + $"_copy{i}.zip");
        File.Copy(source.File, file);
        var manifest = source.Manifest;
        manifest = new SaveManifest
        {
            GameId = game.Id,
            Game = game.Title,
            Platform = game.Platform,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            Created = DateTimeOffset.Now,
            Label = label,
            Auto = false,
            Fingerprint = manifest.Fingerprint,
            Locations = manifest.Locations,
        };
        using (var zip = ZipFile.Open(file, ZipArchiveMode.Update))
        {
            zip.GetEntry(ManifestName)?.Delete();
            using var s = zip.CreateEntry(ManifestName).Open();
            JsonSerializer.Serialize(s, manifest, Json);
        }
        return new SaveSnapshot(file, manifest);
    }

    public void Delete(SaveSnapshot snapshot) => File.Delete(snapshot.File);
}
