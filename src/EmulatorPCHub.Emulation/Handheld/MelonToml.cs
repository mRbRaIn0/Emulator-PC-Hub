using System.Globalization;
using System.Text;

namespace EmulatorPCHub.Emulation.Handheld;

/// <summary>
/// Minimaler Editor für <c>melonDS.toml</c>: setzt einzelne Schlüssel in Tabellen wie <c>[Instance0.Joystick]</c>.
/// Reihenfolge, Kommentare und unbekannte Zeilen bleiben erhalten; fehlende Tabellen/Schlüssel werden angehängt.
/// </summary>
public sealed class MelonToml
{
    private readonly List<string> _lines;

    private MelonToml(List<string> lines) => _lines = lines;

    public static MelonToml Load(string file) =>
        new(File.Exists(file) ? File.ReadAllLines(file).ToList() : []);

    public void Save(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Join("\n", _lines) + "\n", new UTF8Encoding(false));
    }

    /// <summary>Rohwert (bei Zeichenketten ohne Anführungszeichen) oder null.</summary>
    public string? Get(string table, string key)
    {
        var i = Find(table, key);
        if (i < 0)
            return null;
        var v = _lines[i][(_lines[i].IndexOf('=') + 1)..].Trim();
        return v.Length >= 2 && v[0] == '"' && v[^1] == '"' ? Unescape(v[1..^1]) : v;
    }

    public int? GetInt(string table, string key) =>
        int.TryParse(Get(table, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    public void Set(string table, string key, int value) => SetRaw(table, key, value.ToString(CultureInfo.InvariantCulture));

    public void Set(string table, string key, string value) =>
        SetRaw(table, key, "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");

    private void SetRaw(string table, string key, string raw)
    {
        var line = $"{key} = {raw}";
        var i = Find(table, key);
        if (i >= 0)
        {
            _lines[i] = line;
            return;
        }
        var start = TableStart(table);
        if (start < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Length > 0)
                _lines.Add("");
            _lines.Add($"[{table}]");
            _lines.Add(line);
            return;
        }
        // Hinter dem letzten Schlüssel der Tabelle einfügen
        var insert = start + 1;
        for (var j = start + 1; j < _lines.Count && !IsHeader(_lines[j]); j++)
            if (_lines[j].Trim().Length > 0)
                insert = j + 1;
        _lines.Insert(insert, line);
    }

    private static bool IsHeader(string line)
    {
        var t = line.Trim();
        return t.StartsWith('[') && t.EndsWith(']');
    }

    private int TableStart(string table)
    {
        for (var i = 0; i < _lines.Count; i++)
            if (IsHeader(_lines[i]) && _lines[i].Trim()[1..^1].Trim() == table)
                return i;
        return -1;
    }

    private int Find(string table, string key)
    {
        // Schlüssel vor der ersten Tabelle gehören zur Wurzel (table = "")
        var start = table.Length == 0 ? -1 : TableStart(table);
        if (table.Length > 0 && start < 0)
            return -1;
        for (var i = start + 1; i < _lines.Count && !IsHeader(_lines[i]); i++)
        {
            var eq = _lines[i].IndexOf('=');
            if (eq > 0 && _lines[i][..eq].Trim() == key)
                return i;
        }
        return -1;
    }

    private static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");
}
