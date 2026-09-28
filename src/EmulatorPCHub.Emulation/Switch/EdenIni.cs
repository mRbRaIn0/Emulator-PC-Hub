using System.Text;

namespace EmulatorPCHub.Emulation.Switch;

/// <summary>
/// Minimaler Leser/Schreiber für Edens <c>qt-config.ini</c> (SimpleIni im QSettings-Stil):
/// Abschnitte wie <c>[Controls]</c>, Schlüssel mit <c>\</c> als Gruppentrenner, Arrays als
/// <c>name\1\feld</c> + <c>name\size</c> und pro Einstellung <c>key\default=true|false</c>.
/// Reihenfolge und unbekannte Zeilen bleiben erhalten.
/// </summary>
public sealed class EdenIni
{
    // Zeichen, bei denen Eden Werte in Anführungszeichen schreibt (Config::special_characters)
    private const string Special = "!#$%^&*|;'\",<>?`~=";

    private readonly List<(string Name, List<string> Lines)> _sections = [];

    public static EdenIni Load(string file)
    {
        var ini = new EdenIni();
        if (!File.Exists(file))
            return ini;
        List<string>? current = null;
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = [];
                ini._sections.Add((line[1..^1], current));
                continue;
            }
            if (current == null)
            {
                current = [];
                ini._sections.Add(("", current));
            }
            current.Add(line);
        }
        return ini;
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var sb = new StringBuilder();
        foreach (var (name, lines) in _sections)
        {
            if (name.Length > 0)
                sb.Append('[').Append(name).Append("]\n");
            // Leere Zeilen am Abschnittsende zusammenfassen
            var trimmed = lines.ToList();
            while (trimmed.Count > 0 && trimmed[^1].Length == 0)
                trimmed.RemoveAt(trimmed.Count - 1);
            foreach (var l in trimmed)
                sb.Append(l).Append('\n');
            sb.Append('\n');
        }
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
    }

    private List<string> Section(string section, bool create)
    {
        foreach (var (name, lines) in _sections)
            if (name.Equals(section, StringComparison.Ordinal))
                return lines;
        var added = new List<string>();
        if (create)
            _sections.Add((section, added));
        return added;
    }

    private static int IndexOf(List<string> lines, string key)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var eq = lines[i].IndexOf('=');
            if (eq > 0 && lines[i].AsSpan(0, eq).Trim().Equals(key, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    /// <summary>Rohwert (ohne Anführungszeichen) oder null.</summary>
    public string? Get(string section, string key)
    {
        var lines = Section(section, false);
        var i = IndexOf(lines, key);
        if (i < 0)
            return null;
        var v = lines[i][(lines[i].IndexOf('=') + 1)..].Trim();
        return v.Replace("\"", "");
    }

    public void SetRaw(string section, string key, string value)
    {
        var lines = Section(section, true);
        var line = $"{key}={value}";
        var i = IndexOf(lines, key);
        if (i >= 0)
            lines[i] = line;
        else
            lines.Add(line);
    }

    public void Remove(string section, string key)
    {
        var lines = Section(section, false);
        var i = IndexOf(lines, key);
        if (i >= 0)
            lines.RemoveAt(i);
    }

    /// <summary>Schreibt eine Einstellung wie Eden: <c>key\default=false</c> und den Wert (Pfade mit „/“, ggf. in Anführungszeichen).</summary>
    public void Set(string section, string key, string value, bool writeDefaultFlag = true)
    {
        if (writeDefaultFlag)
            SetRaw(section, key + @"\default", "false");
        SetRaw(section, key, Quote(value));
    }

    public void Set(string section, string key, bool value, bool writeDefaultFlag = true) =>
        Set(section, key, value ? "true" : "false", writeDefaultFlag);

    public void Set(string section, string key, int value, bool writeDefaultFlag = true) =>
        Set(section, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture), writeDefaultFlag);

    /// <summary>Setzt eine Einstellung auf Edens Standardwert zurück (<c>key\default=true</c>).</summary>
    public void ResetToDefault(string section, string key)
    {
        SetRaw(section, key + @"\default", "true");
        Remove(section, key);
    }

    public static string Quote(string value)
    {
        var v = value.Replace('\\', '/');
        return v.IndexOfAny(Special.ToCharArray()) >= 0 ? $"\"{v}\"" : v;
    }

    /// <summary>Liest ein Array (<c>prefix\1\field</c> … <c>prefix\size</c>) – ein Feld pro Eintrag.</summary>
    public List<string> GetArray(string section, string prefix, string field)
    {
        var result = new List<string>();
        if (!int.TryParse(Get(section, prefix + @"\size"), out var size))
            return result;
        for (var i = 1; i <= size; i++)
            result.Add(Get(section, $@"{prefix}\{i}\{field}") ?? "");
        return result;
    }

    /// <summary>Hängt einen Eintrag an ein Array an (weitere Felder als Rohwerte).</summary>
    public void AppendArray(string section, string prefix, IReadOnlyList<(string Field, string Value)> fields)
    {
        var size = int.TryParse(Get(section, prefix + @"\size"), out var s) ? s : 0;
        size++;
        foreach (var (field, value) in fields)
            SetRaw(section, $@"{prefix}\{size}\{field}", value);
        SetRaw(section, prefix + @"\size", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
