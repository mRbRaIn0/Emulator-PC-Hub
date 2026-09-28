namespace EmulatorPCHub.Core;

/// <summary>
/// Zentrale Pfade (Plan Abschnitte 12, 24, 27, 28).
/// Standard ist ein portables Layout neben der EXE. Ist der Ordner schreibgeschützt
/// (z. B. unter Program Files), wird %LOCALAPPDATA%\EmulatorPCHub verwendet.
/// </summary>
public sealed class AppPaths
{
    /// <summary>Markiert den Hub-Stammordner (portables Layout).</summary>
    public const string MarkerFile = "EmulatorPCHub.root";

    public string Root { get; }
    public string Data => Path.Combine(Root, "data");
    public string LibraryDb => Path.Combine(Data, "library.db");
    public string ConfigFile => Path.Combine(Data, "config.json");
    public string Artwork => Path.Combine(Data, "artwork");
    public string Profiles => Path.Combine(Data, "profiles");
    public string Presets => Path.Combine(Data, "presets");
    public string Cache => Path.Combine(Data, "cache");
    public string LaunchCache => Path.Combine(Cache, "launch");
    public string Backups => Path.Combine(Root, "Backups");
    public string Logs => Path.Combine(Root, "Logs");
    public string Integrations => Path.Combine(Root, "integrations");
    public string Library => Path.Combine(Root, "Library");

    public AppPaths(string root)
    {
        Root = root;
    }

    public static AppPaths Detect(string? overrideRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return new AppPaths(overrideRoot);
        var env = Environment.GetEnvironmentVariable("EPCHUB_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return new AppPaths(env);

        var exeDir = AppContext.BaseDirectory;

        // Hub-Stammordner mit Markerdatei (data/, integrations/, Backups/, Logs/ liegen daneben).
        var dir = new DirectoryInfo(exeDir);
        for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, MarkerFile)))
                return new AppPaths(dir.FullName);
        }
        if (IsWritable(exeDir))
            return new AppPaths(exeDir);

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmulatorPCHub");
        return new AppPaths(local);
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
