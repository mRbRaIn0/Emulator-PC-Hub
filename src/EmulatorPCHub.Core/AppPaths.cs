namespace EmulatorPCHub.Core;

/// <summary>
/// Zentrale Pfade (Plan Abschnitte 12, 24, 27, 28).
/// Standard ist ein portables Layout neben der EXE. Ist der Ordner schreibgeschützt
/// (z. B. unter Program Files), wird %LOCALAPPDATA%\EmulatorPCHub verwendet.
/// </summary>
public sealed class AppPaths
{
    // Speicherorte (data/backups/logs) stehen in paths.json neben der EXE, nicht in data\config.json: Die Konfiguration liegt ja selbst in data.
    /// <summary>Markiert den Hub-Stammordner (portables Layout).</summary>
    public const string MarkerFile = "EmulatorPCHub.root";

    public string Root { get; }
    /// <summary>Hub-Daten (Konfiguration, Bibliothek, Profile, Miis, Cover) – lokal oder auf dem NAS (<c>paths.json</c>).</summary>
    public string Data { get; }
    public string LibraryDb => Path.Combine(Data, "library.db");
    public string ConfigFile => Path.Combine(Data, "config.json");
    public string Artwork => Path.Combine(Data, "artwork");
    public string Profiles => Path.Combine(Data, "profiles");
    public string Presets => Path.Combine(Data, "presets");
    public string Cache => Path.Combine(Data, "cache");
    public string LaunchCache => Path.Combine(Cache, "launch");
    /// <summary>Backups und Spielstand-Snapshots – lokal oder auf dem NAS.</summary>
    public string Backups { get; }
    public string Logs { get; }
    /// <summary>Hinweis, falls ein eingestellter Speicherort nicht erreichbar war und der lokale Standard genutzt wird.</summary>
    public string? Notice { get; }
    public string Integrations => Path.Combine(Root, "integrations");
    public string Library => Path.Combine(Root, "Library");

    public AppPaths(string root, PathOverrides? overrides = null)
    {
        Root = root;
        var notes = new List<string>();
        string Pick(string? wanted, string standard, string label)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                return standard;
            var ok = overrides is not null && Net.NasLink.Resolve(wanted, overrides.NasHosts) is { } p && Directory.Exists(Path.GetPathRoot(p))
                ? p : null;
            if (ok == null)
                notes.Add($"{label}: {wanted}");
            return ok ?? standard;
        }
        Data = Pick(overrides?.Data, Path.Combine(root, "data"), "Hub-Daten");
        Backups = Pick(overrides?.Backups, Path.Combine(root, "Backups"), "Backups");
        Logs = Pick(overrides?.Logs, Path.Combine(root, "Logs"), "Logs");
        if (notes.Count > 0)
            Notice = "Speicherort nicht erreichbar – lokaler Standard wird genutzt: " + string.Join("; ", notes);
    }

    private static AppPaths Create(string root) => new(root, PathOverrides.Load(root));

    public static AppPaths Detect(string? overrideRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return Create(overrideRoot);
        var env = Environment.GetEnvironmentVariable("EPCHUB_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Create(env);

        var exeDir = AppContext.BaseDirectory;

        // Hub-Stammordner mit Markerdatei (data/, integrations/, Backups/, Logs/ liegen daneben).
        var dir = new DirectoryInfo(exeDir);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, MarkerFile)))
                return Create(dir.FullName);
        }
        if (IsWritable(exeDir))
            return Create(exeDir);

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmulatorPCHub");
        return Create(local);
    }

    public void EnsureCreated()
    {
        foreach (var dir in new[]
                 {
                     Data, Artwork, Profiles, Presets, Cache, LaunchCache, Logs,
                     Path.Combine(Backups, "Saves"), Path.Combine(Backups, "Mods"),
                     Path.Combine(Backups, "Config"), Path.Combine(Backups, "Presets"),
                 })
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string IntegrationDir(string componentId) => Path.Combine(Integrations, componentId);

    private static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Abweichende Speicherorte (z. B. auf dem NAS), gespeichert als <c>paths.json</c> im Hub-Stammordner.
/// <see cref="NasHosts"/>: Adressen desselben NAS (Heimnetz, Tailscale), damit der Ordner unterwegs gefunden wird.
/// Leer = lokaler Standard. Änderungen gelten ab dem nächsten Start.
/// </summary>
public sealed class PathOverrides
{
    [System.Text.Json.Serialization.JsonPropertyName("data")] public string Data { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("backups")] public string Backups { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("logs")] public string Logs { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("nasHosts")] public List<string> NasHosts { get; set; } = [];

    public static string FileFor(string root) => Path.Combine(root, "paths.json");

    public static PathOverrides Load(string root)
    {
        try
        {
            var file = FileFor(root);
            return File.Exists(file)
                ? System.Text.Json.JsonSerializer.Deserialize<PathOverrides>(File.ReadAllText(file), Config.HubJson.Options) ?? new()
                : new();
        }
        catch (Exception)
        {
            return new(); // defekte Datei: lokaler Standard
        }
    }

    public void Save(string root) =>
        File.WriteAllText(FileFor(root), System.Text.Json.JsonSerializer.Serialize(this, Config.HubJson.Options));

    /// <summary>Kopiert einen Ordner samt Inhalt; vorhandene Dateien im Ziel bleiben unberührt, gesperrte Dateien werden übersprungen.</summary>
    public static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            var target = Path.Combine(to, Path.GetFileName(file));
            if (File.Exists(target))
                continue;
            try { File.Copy(file, target); }
            catch (IOException) { /* z. B. gerade geöffnete Logdatei */ }
        }
        foreach (var dir in Directory.EnumerateDirectories(from))
            CopyFolder(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
