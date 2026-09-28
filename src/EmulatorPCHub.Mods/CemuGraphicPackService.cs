using System.Xml.Linq;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Emulation.Cemu;

namespace EmulatorPCHub.Mods;

public sealed record GraphicPack(string Name, string RulesFile, string RelativePath, IReadOnlyList<string> TitleIds, bool Enabled);

/// <summary>Cemu Graphic Packs anzeigen und aktivieren/deaktivieren (Plan 5.3 / 13).</summary>
public sealed class CemuGraphicPackService
{
    private readonly CemuAdapter _cemu;
    private readonly BackupService _backups;

    public CemuGraphicPackService(CemuAdapter cemu, BackupService backups)
    {
        _cemu = cemu;
        _backups = backups;
    }

    public IReadOnlyList<GraphicPack> List(string? titleIdFilter = null)
    {
        var dir = _cemu.GraphicPacksDirectory();
        if (!Directory.Exists(dir))
            return [];
        var enabled = EnabledEntries();
        var result = new List<GraphicPack>();
        foreach (var rules in Directory.EnumerateFiles(dir, "rules.txt", SearchOption.AllDirectories))
        {
            var (name, titles) = ParseRules(rules);
            if (titleIdFilter != null && !titles.Contains(titleIdFilter, StringComparer.OrdinalIgnoreCase))
                continue;
            var rel = "graphicPacks/" + Path.GetRelativePath(dir, rules).Replace('\\', '/');
            result.Add(new GraphicPack(name ?? Path.GetFileName(Path.GetDirectoryName(rules)!), rules, rel, titles,
                enabled.Contains(rel, StringComparer.OrdinalIgnoreCase)));
        }
        return result.OrderBy(g => g.Name).ToList();
    }

    private static (string? name, List<string> titles) ParseRules(string file)
    {
        string? name = null;
        var titles = new List<string>();
        var inDefinition = false;
        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                if (inDefinition)
                    break;
                inDefinition = line.Equals("[Definition]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inDefinition)
                continue;
            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                name = value;
            else if (key.Equals("titleIds", StringComparison.OrdinalIgnoreCase))
                titles.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        return (name, titles);
    }

    private HashSet<string> EnabledEntries()
    {
        var file = _cemu.SettingsFile();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(file))
            return set;
        try
        {
            foreach (var e in XDocument.Load(file).Root?.Element("GraphicPack")?.Elements("Entry") ?? [])
            {
                var f = e.Attribute("filename")?.Value;
                if (f != null && e.Attribute("disabled")?.Value != "true")
                    set.Add(f.Replace('\\', '/'));
            }
        }
        catch (Exception)
        {
        }
        return set;
    }

    /// <summary>Aktiviert/deaktiviert ein Graphic Pack in Cemus settings.xml (mit Backup).</summary>
    public void SetEnabled(GraphicPack pack, bool enabled)
    {
        var file = _cemu.SettingsFile();
        _backups.BackupFile(BackupCategory.Config, file, "cemu-settings");
        var doc = File.Exists(file) ? XDocument.Load(file) : new XDocument(new XElement("content"));
        var gp = doc.Root!.Element("GraphicPack");
        if (gp == null)
            doc.Root.Add(gp = new XElement("GraphicPack"));
        var entry = gp.Elements("Entry").FirstOrDefault(e =>
            string.Equals(e.Attribute("filename")?.Value?.Replace('\\', '/'), pack.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (enabled && entry == null)
            gp.Add(new XElement("Entry", new XAttribute("filename", pack.RelativePath)));
        else if (!enabled)
            entry?.Remove();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        doc.Save(file);
    }
}
