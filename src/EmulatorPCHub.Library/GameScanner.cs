using System.Security.Cryptography;
using System.Text;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>Ein zu scannender Ordner; Plattform null = automatisch erkennen.</summary>
public sealed record ScanRoot(string Path, HubPlatform? Platform);

public sealed class ScanResult
{
    public List<GameEntry> Games { get; } = [];
    /// <summary>Updates/DLCs je Basis-Title-ID (Switch).</summary>
    public Dictionary<string, SwitchAddOns> SwitchAddOns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; } = [];
    public int FilesChecked { get; set; }
}

/// <summary>
/// Scannt Bibliotheksordner (Plan Abschnitt 11). Spiele müssen nicht in den Hub-Ordner kopiert werden –
/// bestehende Speicherorte werden direkt eingebunden.
/// </summary>
public sealed class GameScanner
{
    private static readonly HashSet<string> DiscExt = new(StringComparer.OrdinalIgnoreCase)
        { ".iso", ".gcm", ".gcz", ".rvz", ".ciso", ".wia", ".wbfs" };
    private static readonly HashSet<string> WiiUExt = new(StringComparer.OrdinalIgnoreCase) { ".wux", ".wud", ".wua" };
    private static readonly HashSet<string> SwitchExt = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".nsz", ".xci", ".xcz" };
    private static readonly HashSet<string> DsExt = new(HubPlatform.DS.FileExtensions(), StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ThreeDsExt = new(HubPlatform.ThreeDS.FileExtensions(), StringComparer.OrdinalIgnoreCase);

    public int MaxDepth { get; set; } = 5;

    public ScanResult Scan(IEnumerable<ScanRoot> roots, CancellationToken ct = default)
    {
        var result = new ScanResult();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root.Path) || !Directory.Exists(root.Path))
                continue;
            foreach (var file in EnumerateFiles(root.Path, 0, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!seenPaths.Add(file))
                    continue;
                result.FilesChecked++;
                try
                {
                    AnalyzeFile(file, root.Platform, result);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{file}: {ex.Message}");
                    HubLog.Warn($"Scan-Fehler bei {file}", ex);
                }
            }
        }

        // Gleiches Spiel in mehreren Formaten (z. B. .wud + .wux oder .iso + .rvz): nur einen Eintrag behalten.
        var deduped = result.Games
            .GroupBy(g => g.Id)
            .Select(g => g.OrderBy(x => FormatRank(x.Path)).First())
            .ToList();
        result.Games.Clear();
        result.Games.AddRange(deduped);
        return result;
    }

    private IEnumerable<string> EnumerateFiles(string dir, int depth, CancellationToken ct)
    {
        if (depth > MaxDepth || ct.IsCancellationRequested)
            yield break;
        string[] files;
        string[] dirs;
        try
        {
            files = Directory.GetFiles(dir);
            dirs = Directory.GetDirectories(dir);
        }
        catch (Exception)
        {
            yield break;
        }
        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            if (DiscExt.Contains(ext) || WiiUExt.Contains(ext) || SwitchExt.Contains(ext) || DsExt.Contains(ext) || ThreeDsExt.Contains(ext) ||
                (ext.Equals(".rpx", StringComparison.OrdinalIgnoreCase) &&
                 Path.GetFileName(Path.GetDirectoryName(f))?.Equals("code", StringComparison.OrdinalIgnoreCase) == true))
                yield return f;
        }
        foreach (var d in dirs)
        {
            var name = Path.GetFileName(d);
            if (name.StartsWith('.') || name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var f in EnumerateFiles(d, depth + 1, ct))
                yield return f;
        }
    }

    private static void AnalyzeFile(string file, HubPlatform? rootPlatform, ScanResult result)
    {
        var ext = Path.GetExtension(file);
        if (DiscExt.Contains(ext))
        {
            var info = DiscHeaderReader.Read(file);
            var platform = info?.Platform ?? rootPlatform ?? (ext.Equals(".wbfs", StringComparison.OrdinalIgnoreCase) ? HubPlatform.Wii : null);
            if (platform is not (HubPlatform.Wii or HubPlatform.GameCube))
                return;
            var title = TitleDatabase.LookupDisc(info?.GameCode)
                        ?? (string.IsNullOrWhiteSpace(info?.HeaderTitle) ? TitleDatabase.CleanFileName(file) : info!.HeaderTitle!);
            AddGame(result, file, platform.Value, info?.GameCode, title, info?.Size ?? SafeSize(file), null);
            return;
        }

        if (WiiUExt.Contains(ext) || ext.Equals(".rpx", StringComparison.OrdinalIgnoreCase))
        {
            if (rootPlatform is not null and not HubPlatform.WiiU)
                return;
            string title;
            string? code = null;
            Dictionary<string, string>? localized = null;
            if (ext.Equals(".rpx", StringComparison.OrdinalIgnoreCase))
            {
                var meta = WiiUMetaReader.ReadFromRpx(file);
                title = meta?.HeaderTitle ?? TitleDatabase.CleanFileName(file);
                code = meta?.GameCode;
                localized = meta?.LocalizedTitles;
                // Updates (0005000E) und DLCs (0005000C) sind keine eigenen Spiele
                if (code is { Length: 16 } && (code.StartsWith("0005000E") || code.StartsWith("0005000C")))
                    return;
            }
            else
            {
                title = TitleDatabase.CleanFileName(file);
            }
            AddGame(result, file, HubPlatform.WiiU, code, title, SafeSize(file), localized);
            return;
        }

        if (DsExt.Contains(ext) || ThreeDsExt.Contains(ext))
        {
            var platform = DsExt.Contains(ext) ? HubPlatform.DS : HubPlatform.ThreeDS;
            if (rootPlatform is not null && rootPlatform != platform)
                return;
            var code = platform == HubPlatform.DS ? ReadDsGameCode(file) : ReadThreeDsTitleId(file);
            AddGame(result, file, platform, code, TitleDatabase.CleanFileName(file), SafeSize(file), null);
            return;
        }

        if (SwitchExt.Contains(ext))
        {
            if (rootPlatform is not null and not HubPlatform.Switch)
                return;
            var info = SwitchFileReader.Read(file);
            if (info.SwitchKind is SwitchContentKind.Update or SwitchContentKind.Dlc && info.BaseTitleId != null)
            {
                if (!result.SwitchAddOns.TryGetValue(info.BaseTitleId, out var addons))
                    result.SwitchAddOns[info.BaseTitleId] = addons = new SwitchAddOns();
                (info.SwitchKind == SwitchContentKind.Update ? addons.Updates : addons.Dlcs).Add(info);
                return;
            }
            var title = TitleDatabase.LookupSwitch(info.TitleId) ?? TitleDatabase.CleanFileName(file);
            AddGame(result, file, HubPlatform.Switch, info.TitleId, title, info.Size);
        }
    }

    private static void AddGame(ScanResult result, string file, HubPlatform platform, string? code, string title, long size,
        Dictionary<string, string>? localizedTitles = null)
    {
        var special = TitleDatabase.DetectSpecial(platform, code, title);
        var key = !string.IsNullOrEmpty(code) ? code.ToUpperInvariant()
            : platform == HubPlatform.WiiU ? "t-" + Slug(title)
            : ShortHash(file);
        var id = platform.ConfigKey() + ":" + key;
        result.Games.Add(new GameEntry
        {
            Id = id,
            Title = title,
            Platform = platform,
            Path = file,
            GameCode = code,
            EmulatorId = platform.DefaultAdapterId(),
            Special = special,
            FileSize = size,
            Languages = DetectLanguages(platform, code, localizedTitles),
        });
    }

    /// <summary>Sprachen eines Spiels: Wii U aus meta.xml, GameCube/Wii aus dem Regionsbuchstaben der Disc-ID.</summary>
    private static GameLanguageData DetectLanguages(HubPlatform platform, string? code, Dictionary<string, string>? localizedTitles)
    {
        var data = new GameLanguageData();
        if (localizedTitles != null)
        {
            foreach (var (lang, t) in localizedTitles)
            {
                data.DetectedTitles[lang] = t;
                data.Detected.Add(lang);
            }
        }
        else if (platform is HubPlatform.GameCube or HubPlatform.Wii && code is { Length: >= 4 })
        {
            // PAL-Discs ('P') sind mehrsprachig – welche Sprachen genau, steht nicht im Header.
            var lang = char.ToUpperInvariant(code[3]) switch
            {
                'E' or 'U' => "en", 'J' => "ja", 'K' => "ko", 'D' => "de", 'F' => "fr", 'S' => "es", 'I' => "it", 'H' => "nl", _ => null,
            };
            if (lang != null)
                data.Detected.Add(lang);
        }
        return data;
    }

    /// <summary>Bevorzugte Formate zuerst (komprimiert/vollständig vor Rohformaten).</summary>
    private static int FormatRank(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".wua" => 0,
        ".rvz" => 0,
        ".nsp" => 0,
        ".wux" => 1,
        ".iso" => 1,
        ".xci" => 1,
        ".gcm" => 2,
        ".wbfs" => 2,
        ".nsz" => 2,
        ".wud" => 3,
        ".ciso" => 3,
        ".gcz" => 4,
        _ => 5,
    };

    private static string Slug(string title) =>
        new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    /// <summary>DS-ROM-Header: Spielcode (4 Zeichen, z. B. AMCP) bei 0x0C.</summary>
    internal static string? ReadDsGameCode(string file)
    {
        try
        {
            using var fs = File.OpenRead(file);
            var header = new byte[0x10];
            if (fs.Read(header, 0, header.Length) < header.Length)
                return null;
            var code = Encoding.ASCII.GetString(header, 0x0C, 4);
            return code.All(char.IsLetterOrDigit) ? code : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// 3DS-Title-ID (16 Hex-Zeichen) aus dem unverschlüsselten Kopf: NCSD (.3ds/.cci) bzw. NCCH (.cxi),
    /// Magic bei 0x100, Media-/Partitions-ID (little endian) bei 0x108. Komprimierte/3DSX-Dateien: null.
    /// </summary>
    internal static string? ReadThreeDsTitleId(string file)
    {
        try
        {
            using var fs = File.OpenRead(file);
            var header = new byte[0x110];
            if (fs.Read(header, 0, header.Length) < header.Length)
                return null;
            var magic = Encoding.ASCII.GetString(header, 0x100, 4);
            if (magic is not ("NCSD" or "NCCH"))
                return null;
            var id = BitConverter.ToUInt64(header, 0x108);
            return id == 0 ? null : id.ToString("X16");
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static long SafeSize(string file)
    {
        try { return new FileInfo(file).Length; }
        catch (IOException) { return 0; }
    }

    public static string ShortHash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s.ToLowerInvariant())))[..10];
}
