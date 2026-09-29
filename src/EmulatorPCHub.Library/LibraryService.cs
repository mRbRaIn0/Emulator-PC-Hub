using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>
/// Spielebibliothek: kombiniert Scan-Ergebnisse mit der Datenbank (Favoriten, Spielzeit, Presets bleiben erhalten)
/// und sorgt dafür, dass Mario Kart Wii / Mario Kart 8 Deluxe als Spezialeinträge erscheinen.
/// </summary>
public sealed class LibraryService
{
    private readonly LibraryDatabase _db;
    private readonly AppPaths _paths;
    private readonly ConfigService _config;
    private readonly object _lock = new();
    private List<GameEntry> _games = [];

    public LibraryDatabase Database => _db;
    public ScanResult? LastScan { get; private set; }
    public IReadOnlyDictionary<string, SwitchAddOns> SwitchAddOns =>
        LastScan?.SwitchAddOns ?? new Dictionary<string, SwitchAddOns>();

    /// <summary>Alle erkannten Mario-Kart-Wii-Dumps (für die Engine-Auswahl).</summary>
    public List<GameEntry> MarioKartWiiDumps { get; } = [];
    public List<GameEntry> MarioKart8DeluxeDumps { get; } = [];

    public event EventHandler? Changed;

    public LibraryService(LibraryDatabase db, AppPaths paths, ConfigService config)
    {
        _db = db;
        _paths = paths;
        _config = config;
        _games = _db.LoadGames();
        EnsureSpecialEntries();
    }

    public IReadOnlyList<GameEntry> Games
    {
        get
        {
            lock (_lock)
                return _games.ToList();
        }
    }

    public GameEntry? Find(string id)
    {
        lock (_lock)
            return _games.FirstOrDefault(g => g.Id == id);
    }

    public IEnumerable<ScanRoot> ConfiguredRoots()
    {
        var cfg = _config.Current;
        foreach (var p in HubPlatformInfo.Emulated)
        {
            var path = cfg.LibraryPaths.Get(p);
            if (!string.IsNullOrWhiteSpace(path))
                yield return new ScanRoot(path, p);
        }
        foreach (var extra in cfg.ExtraLibraryFolders)
            yield return new ScanRoot(extra, null);
        // Standard-Bibliothek neben dem Hub (Plan: Library/GameCube, Wii, WiiU, Switch)
        foreach (var (folder, platform) in new[]
                 {
                     ("GameCube", HubPlatform.GameCube), ("Wii", HubPlatform.Wii),
                     ("WiiU", HubPlatform.WiiU), ("Switch", HubPlatform.Switch),
                     ("DS", HubPlatform.DS), ("3DS", HubPlatform.ThreeDS),
                 })
        {
            var p = Path.Combine(_paths.Library, folder);
            if (Directory.Exists(p))
                yield return new ScanRoot(p, platform);
        }
    }

    public Task<ScanResult> RefreshAsync(IEnumerable<ScanRoot> additionalRoots, CancellationToken ct = default) =>
        Task.Run(() => Refresh(additionalRoots, ct), ct);

    public ScanResult Refresh(IEnumerable<ScanRoot> additionalRoots, CancellationToken ct = default)
    {
        var roots = ConfiguredRoots().Concat(additionalRoots).ToList();
        HubLog.Info($"Bibliothek wird gescannt ({roots.Count} Ordner)");
        var scan = new GameScanner().Scan(roots, ct);
        LastScan = scan;

        var existing = _db.LoadGames(includeMissing: true).ToDictionary(g => g.Id);
        var merged = new List<GameEntry>();
        MarioKartWiiDumps.Clear();
        MarioKart8DeluxeDumps.Clear();

        foreach (var found in scan.Games)
        {
            if (found.Special == SpecialPage.MarioKartWii)
            {
                MarioKartWiiDumps.Add(found);
                continue;
            }
            if (found.Special == SpecialPage.MarioKart8Deluxe)
            {
                MarioKart8DeluxeDumps.Add(found);
                continue;
            }
            if (existing.TryGetValue(found.Id, out var old))
            {
                if (!old.CustomTitle)
                    old.Title = found.Title;
                old.Path = found.Path;
                old.GameCode = found.GameCode;
                old.Platform = found.Platform;
                old.FileSize = found.FileSize;
                old.Languages.Detected = found.Languages.Detected;
                old.Languages.DetectedTitles = found.Languages.DetectedTitles;
                if (string.IsNullOrEmpty(old.EmulatorId) || old.EmulatorId == EmulatorIds.BuiltIn)
                    old.EmulatorId = found.EmulatorId;
                merged.Add(old);
            }
            else
            {
                merged.Add(found);
            }
        }

        foreach (var g in merged)
            ResolveArtwork(g);

        _db.UpsertMany(merged);
        var foundIds = merged.Select(g => g.Id).ToHashSet();
        var missing = existing.Keys.Where(id => !foundIds.Contains(id) && !id.StartsWith("special:")).ToList();
        _db.MarkMissing(missing);

        lock (_lock)
            _games = _db.LoadGames();
        EnsureSpecialEntries();
        ApplyFavorites();
        HubLog.Info($"Scan fertig: {merged.Count} Spiele, {scan.FilesChecked} Dateien geprüft, " +
                    $"{MarioKartWiiDumps.Count} MKWii-Dump(s), {MarioKart8DeluxeDumps.Count} MK8DX-Dump(s)");
        Changed?.Invoke(this, EventArgs.Empty);
        return scan;
    }

    /// <summary>Mario Kart Wii und Mario Kart 8 Deluxe haben feste Einträge mit Spezialseite.</summary>
    private void EnsureSpecialEntries()
    {
        var cfg = _config.Current;
        var changed = new List<GameEntry>();
        GameEntry Ensure(string id, string title, HubPlatform platform, SpecialPage page, string emulator)
        {
            var e = Find(id);
            if (e == null)
            {
                e = new GameEntry { Id = id, Title = title, Platform = platform, Special = page, EmulatorId = emulator, IsPlaceholder = true };
                lock (_lock)
                    _games.Add(e);
            }
            changed.Add(e);
            return e;
        }

        var mkwii = Ensure(KnownGames.MarioKartWiiId, "Mario Kart Wii", HubPlatform.Wii, SpecialPage.MarioKartWii,
            cfg.MarioKartWii.Engine == "dolphin" ? EmulatorIds.Dolphin : EmulatorIds.WiiCompiled);
        var mkwiiFile = !string.IsNullOrWhiteSpace(cfg.MarioKartWii.GameFile) && File.Exists(cfg.MarioKartWii.GameFile)
            ? cfg.MarioKartWii.GameFile
            : (MarioKartWiiDumps.FirstOrDefault(d => d.GameCode == "RMCP01") ?? MarioKartWiiDumps.FirstOrDefault())?.Path;
        if (mkwiiFile != null)
        {
            mkwii.Path = mkwiiFile;
            mkwii.GameCode = MarioKartWiiDumps.FirstOrDefault(d => d.Path == mkwiiFile)?.GameCode ?? mkwii.GameCode;
        }
        mkwii.IsPlaceholder = string.IsNullOrEmpty(mkwii.Path) || !File.Exists(mkwii.Path);

        var mk8 = Ensure(KnownGames.MarioKart8DeluxeId, "Mario Kart 8 Deluxe", HubPlatform.Switch, SpecialPage.MarioKart8Deluxe, EmulatorIds.Switch);
        var mk8File = !string.IsNullOrWhiteSpace(cfg.MarioKart8Deluxe.GameFile) && File.Exists(cfg.MarioKart8Deluxe.GameFile)
            ? cfg.MarioKart8Deluxe.GameFile
            : MarioKart8DeluxeDumps.FirstOrDefault()?.Path;
        if (mk8File != null)
        {
            mk8.Path = mk8File;
            mk8.GameCode = KnownGames.MarioKart8DeluxeTitleId;
        }
        mk8.IsPlaceholder = string.IsNullOrEmpty(mk8.Path) || !File.Exists(mk8.Path);

        foreach (var e in changed)
            ResolveArtwork(e);
        _db.UpsertMany(changed);
    }

    public void ResolveArtwork(GameEntry g)
    {
        var dir = ArtworkDir(g);
        g.CoverPath = FirstExisting(dir, "cover") ?? SideBySide(g.Path, "") ?? SideBySide(g.Path, ".cover") ?? g.CoverPath;
        g.BackgroundPath = FirstExisting(dir, "background") ?? SideBySide(g.Path, ".background") ?? g.BackgroundPath;
        g.IconPath = FirstExisting(dir, "icon") ?? g.IconPath;
        foreach (var lang in g.Languages.Covers.Keys.ToList())
        {
            var p = FirstExisting(dir, "cover." + lang);
            if (p == null)
                g.Languages.Covers.Remove(lang);
            else
                g.Languages.Covers[lang] = p;
        }
        if (g.CoverPath != null && !File.Exists(g.CoverPath))
            g.CoverPath = null;
        if (g.BackgroundPath != null && !File.Exists(g.BackgroundPath))
            g.BackgroundPath = null;
    }

    public string ArtworkDir(GameEntry g)
    {
        var safe = string.Concat(g.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
        return Path.Combine(_paths.Artwork, safe);
    }

    private static string? FirstExisting(string dir, string name)
    {
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var p = Path.Combine(dir, name + ext);
            if (File.Exists(p))
                return p;
        }
        return null;
    }

    private static string? SideBySide(string gamePath, string suffix)
    {
        if (string.IsNullOrEmpty(gamePath))
            return null;
        var dir = Path.GetDirectoryName(gamePath);
        if (dir == null)
            return null;
        var baseName = Path.GetFileNameWithoutExtension(gamePath);
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var p = Path.Combine(dir, baseName + suffix + ext);
            if (File.Exists(p))
                return p;
        }
        return null;
    }

    public void Save(GameEntry g)
    {
        _db.Upsert(g);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Eigenes Cover: Bild wird in den Artwork-Ordner des Spiels kopiert (hat dort Vorrang).</summary>
    public void SetCover(GameEntry g, string imageFile, string? language = null)
    {
        var dir = ArtworkDir(g);
        Directory.CreateDirectory(dir);
        var baseName = CoverBaseName(language);
        RemoveFiles(dir, baseName);
        var target = Path.Combine(dir, baseName + Path.GetExtension(imageFile).ToLowerInvariant());
        File.Copy(imageFile, target, overwrite: true);
        if (language == null)
            g.CoverPath = target;
        else
            g.Languages.Covers[language] = target;
        Save(g);
    }

    /// <summary>Dateiname-Stamm: "cover" (Standard) bzw. "cover.de" (Sprachvariante).</summary>
    private static string CoverBaseName(string? language, string prefix = "cover") =>
        string.IsNullOrEmpty(language) ? prefix : prefix + "." + language;

    /// <summary>Originalbild eines eigenen Covers merken, damit der quadratische Ausschnitt später neu gewählt werden kann.</summary>
    public void SetCoverSource(GameEntry g, string imageFile, string? language = null)
    {
        var dir = ArtworkDir(g);
        Directory.CreateDirectory(dir);
        var baseName = CoverBaseName(language, "cover-source");
        var target = Path.Combine(dir, baseName + Path.GetExtension(imageFile).ToLowerInvariant());
        if (string.Equals(Path.GetFullPath(imageFile), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            return;
        RemoveFiles(dir, baseName);
        File.Copy(imageFile, target, overwrite: true);
    }

    /// <summary>Gemerktes Originalbild des Covers (oder null).</summary>
    public string? CoverSourcePath(GameEntry g, string? language = null)
    {
        var dir = ArtworkDir(g);
        var baseName = CoverBaseName(language, "cover-source");
        return CoverExtensions.Select(ext => Path.Combine(dir, baseName + ext)).FirstOrDefault(File.Exists);
    }

    /// <summary>Eigenes Cover entfernen – danach gilt wieder ein Cover neben der Spieldatei (falls vorhanden).</summary>
    public void ResetCover(GameEntry g, string? language = null)
    {
        var dir = ArtworkDir(g);
        RemoveFiles(dir, CoverBaseName(language));
        RemoveFiles(dir, CoverBaseName(language, "cover-source"));
        if (language == null)
            g.CoverPath = null;
        else
            g.Languages.Covers.Remove(language);
        ResolveArtwork(g);
        Save(g);
    }

    private static readonly string[] CoverExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private static void RemoveFiles(string dir, string baseName)
    {
        foreach (var ext in CoverExtensions)
        {
            var p = Path.Combine(dir, baseName + ext);
            if (File.Exists(p))
                File.Delete(p);
        }
    }

    /// <summary>Aktives Hub-Profil: Favoriten, Spielzeit und Statistik werden pro Profil geführt.</summary>
    public string? ActiveProfileId { get; private set; }

    public void SetActiveProfile(string profileId)
    {
        ActiveProfileId = profileId;
        _db.MigrateLegacyFavorites(profileId);
        ApplyFavorites();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFavorites()
    {
        if (ActiveProfileId == null)
            return;
        var favorites = _db.Favorites(ActiveProfileId);
        foreach (var g in Games)
            g.IsFavorite = favorites.Contains(g.Id);
    }

    /// <summary>Favoriten eines beliebigen Profils (für die Profilseite).</summary>
    public int FavoriteCount(string profileId) => _db.Favorites(profileId).Count;

    public void ToggleFavorite(GameEntry g)
    {
        g.IsFavorite = !g.IsFavorite;
        if (ActiveProfileId != null)
            _db.SetFavorite(ActiveProfileId, g.Id, g.IsFavorite);
        Save(g);
    }

    public void RecordLaunch(GameEntry g, DateTimeOffset started, bool success) =>
        _db.AddLaunch(new LaunchAttempt(g.Id, ActiveProfileId, started, success));

    public List<PlaySession> AllSessions() => _db.Sessions(limit: int.MaxValue);
    public List<LaunchAttempt> AllLaunches() => _db.Launches();

    public void RecordSession(GameEntry g, string? preset, string emulator, DateTimeOffset started, TimeSpan duration, int? exitCode)
    {
        _db.AddSession(new PlaySession(g.Id, preset, emulator, started, duration.TotalSeconds, exitCode, ActiveProfileId));
        g.PlayTimeSeconds += (long)Math.Round(duration.TotalSeconds);
        g.LastPlayed = started;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sortierung für den Homescreen: zuletzt gespielt zuerst, dann Favoriten, dann alphabetisch.</summary>
    public IReadOnlyList<GameEntry> HomeOrder()
    {
        var showSpecial = _config.Current.Ui.ShowSpecialTiles;
        return Games
            .Where(g => (showSpecial || !g.IsPlaceholder) && !g.HiddenOnHome)
            .OrderByDescending(g => g.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenByDescending(g => g.Special != SpecialPage.None)
            .ThenByDescending(g => g.IsFavorite)
            .ThenBy(g => g.DisplayTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
