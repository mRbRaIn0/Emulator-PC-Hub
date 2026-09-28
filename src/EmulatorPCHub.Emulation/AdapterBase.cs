using System.Diagnostics;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation;

/// <summary>Gemeinsame Hilfen für Adapter: Suche nach EXEs, Versionen, INI-Dateien.</summary>
public abstract class AdapterBase : IEmulatorAdapter
{
    protected AppPaths Paths { get; }
    protected ConfigService Config { get; }

    protected AdapterBase(AppPaths paths, ConfigService config)
    {
        Paths = paths;
        Config = config;
    }

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyList<HubPlatform> Platforms { get; }
    public abstract EmulatorInstallation DetectInstallation();
    public abstract IReadOnlyList<string> DetectGames();
    public abstract LaunchSpec PrepareLaunch(LaunchRequest request);

    public virtual IReadOnlyDictionary<string, string> GetConfig() => new Dictionary<string, string>();
    public virtual void ApplyConfig(IReadOnlyDictionary<string, string> values) { }

    /// <summary>Erster existierender Pfad; Verzeichnisse werden rekursiv (Tiefe 3) nach dem Dateinamen durchsucht.</summary>
    protected static string? FindExecutable(string fileName, params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c))
                continue;
            var expanded = Environment.ExpandEnvironmentVariables(c);
            if (File.Exists(expanded) && Path.GetFileName(expanded).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return expanded;
            if (File.Exists(expanded) && expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return expanded;
            if (Directory.Exists(expanded))
            {
                var found = SearchDir(expanded, fileName, 0);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private static string? SearchDir(string dir, string fileName, int depth)
    {
        var direct = Path.Combine(dir, fileName);
        if (File.Exists(direct))
            return direct;
        if (depth >= 3)
            return null;
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith('_') || name.Equals("mods-store", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("User", StringComparison.OrdinalIgnoreCase))
                    continue;
                var r = SearchDir(sub, fileName, depth + 1);
                if (r != null)
                    return r;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        return null;
    }

    protected static string? FileVersion(string? exe)
    {
        if (exe == null || !File.Exists(exe))
            return null;
        // Vom Hub-Installer gemerkte Version (manche EXEs haben keine Versionsressource)
        var marker = Path.Combine(Path.GetDirectoryName(exe)!, ComponentVersionFile);
        if (File.Exists(marker))
            return File.ReadAllText(marker).Trim();
        try
        {
            var v = FileVersionInfo.GetVersionInfo(exe);
            var s = v.ProductVersion ?? v.FileVersion;
            if (string.IsNullOrWhiteSpace(s))
                return null;
            var plus = s.IndexOf('+');
            return plus > 0 ? s[..plus] : s;
        }
        catch
        {
            return null;
        }
    }

    public const string ComponentVersionFile = "hub-version.txt";

    protected static string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    protected static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    protected static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    protected static string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}

/// <summary>Minimaler INI-Leser/-Schreiber (Dolphin-Konfiguration), erhält Reihenfolge und Kommentare.</summary>
public sealed class IniFile
{
    private readonly List<string> _lines;
    public string Path { get; }

    public IniFile(string path)
    {
        Path = path;
        _lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
    }

    public string? Get(string section, string key)
    {
        var inSection = false;
        foreach (var raw in _lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSection = line[1..^1].Equals(section, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection)
                continue;
            var eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return line[(eq + 1)..].Trim();
        }
        return null;
    }

    public void Set(string section, string key, string value)
    {
        var sectionStart = -1;
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (sectionStart >= 0)
                {
                    _lines.Insert(i, $"{key} = {value}");
                    return;
                }
                if (line[1..^1].Equals(section, StringComparison.OrdinalIgnoreCase))
                    sectionStart = i;
                continue;
            }
            if (sectionStart >= 0)
            {
                var eq = line.IndexOf('=');
                if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    _lines[i] = $"{key} = {value}";
                    return;
                }
            }
        }
        if (sectionStart < 0)
            _lines.Add($"[{section}]");
        _lines.Add($"{key} = {value}");
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllLines(Path, _lines);
    }
}
