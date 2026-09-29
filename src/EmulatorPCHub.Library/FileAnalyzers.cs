using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>
/// Liest Disc-Header von GameCube-/Wii-Images (ISO, GCM, CISO, WBFS, RVZ/WIA, GCZ).
/// Es wird nur gelesen – nichts verändert.
/// </summary>
public static class DiscHeaderReader
{
    private const uint WiiMagic = 0x5D1C9EA3;
    private const uint GcMagic = 0xC2339F3D;

    public static GameFileInfo? Read(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var head = new byte[0x100];
            if (fs.Read(head, 0, head.Length) < 0x60)
                return null;

            var magic4 = Encoding.ASCII.GetString(head, 0, 4);
            byte[]? disc = null;

            if (magic4 is "RVZ\u0001" or "WIA\u0001")
            {
                // Header 1 (0x48) + Header 2: disc_type, compression, level, chunk_size, dhead[0x80]
                disc = head.AsSpan(0x58, 0x80).ToArray();
            }
            else if (magic4 == "WBFS")
            {
                var secShift = head[8];
                var sec = 1L << secShift;
                disc = ReadAt(fs, sec, 0x80);
            }
            else if (magic4 == "CISO")
            {
                disc = ReadAt(fs, 0x8000, 0x80);
            }
            else if (BinaryPrimitives.ReadUInt32LittleEndian(head) == 0xB10BC001)
            {
                disc = ReadGczFirstBlock(fs, head);
            }
            else
            {
                disc = head;
            }

            if (disc == null || disc.Length < 0x60)
                return null;
            var info = ParseDiscHeader(disc, path);
            if (info != null)
                info.Size = fs.Length;
            return info;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static GameFileInfo? ParseDiscHeader(ReadOnlySpan<byte> disc, string path)
    {
        var wii = BinaryPrimitives.ReadUInt32BigEndian(disc[0x18..]) == WiiMagic;
        var gc = BinaryPrimitives.ReadUInt32BigEndian(disc[0x1C..]) == GcMagic;
        if (!wii && !gc)
            return null;

        var id = Encoding.ASCII.GetString(disc[..6]).TrimEnd('\0');
        if (id.Any(c => !char.IsLetterOrDigit(c)))
            return null;
        var titleBytes = disc.Slice(0x20, Math.Min(0x40, disc.Length - 0x20));
        var end = titleBytes.IndexOf((byte)0);
        var title = Encoding.Latin1.GetString(end >= 0 ? titleBytes[..end] : titleBytes).Trim();
        return new GameFileInfo
        {
            Path = path,
            Platform = wii ? HubPlatform.Wii : HubPlatform.GameCube,
            GameCode = id,
            HeaderTitle = title,
        };
    }

    private static byte[] ReadAt(Stream s, long offset, int count)
    {
        s.Seek(offset, SeekOrigin.Begin);
        var buf = new byte[count];
        var read = s.Read(buf, 0, count);
        return read == count ? buf : buf[..read];
    }

    private static byte[]? ReadGczFirstBlock(Stream fs, byte[] head)
    {
        // GCZ: magic, sub_type, compressed_size(8), data_size(8), block_size(4), num_blocks(4)
        var blockSize = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(24));
        var numBlocks = BinaryPrimitives.ReadUInt32LittleEndian(head.AsSpan(28));
        if (numBlocks == 0 || blockSize == 0 || blockSize > 16 * 1024 * 1024)
            return null;
        var ptrs = ReadAt(fs, 32, 16);
        var p0 = BinaryPrimitives.ReadUInt64LittleEndian(ptrs);
        var p1 = numBlocks > 1 ? BinaryPrimitives.ReadUInt64LittleEndian(ptrs.AsSpan(8)) : 0;
        var dataStart = 32 + numBlocks * 8L + numBlocks * 4L;
        var uncompressed = (p0 & 0x8000000000000000UL) != 0;
        var offset = (long)(p0 & 0x7FFFFFFFFFFFFFFFUL);
        var nextOffset = numBlocks > 1 ? (long)(p1 & 0x7FFFFFFFFFFFFFFFUL) : offset + blockSize;
        var len = (int)Math.Clamp(nextOffset - offset, 0x80, blockSize);
        var raw = ReadAt(fs, dataStart + offset, len);
        if (uncompressed)
            return raw;
        using var z = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
        var buf = new byte[0x100];
        var total = 0;
        while (total < buf.Length)
        {
            var n = z.Read(buf, total, buf.Length - total);
            if (n <= 0)
                break;
            total += n;
        }
        return buf;
    }
}

/// <summary>Liest Title-IDs aus Switch-Containern (NSP/NSZ = PFS0) und Dateinamen.</summary>
public static partial class SwitchFileReader
{
    [GeneratedRegex(@"\[([0-9A-Fa-f]{16})\]")]
    private static partial Regex TitleIdInName();

    [GeneratedRegex(@"\[v(\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex VersionInName();

    public static GameFileInfo Read(string path)
    {
        var info = new GameFileInfo { Path = path, Platform = HubPlatform.Switch };
        try
        {
            info.Size = new FileInfo(path).Length;
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".nsp" or ".nsz")
                ReadPfs0(path, info);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        var name = System.IO.Path.GetFileName(path);
        var tidMatch = TitleIdInName().Match(name);
        if (info.TitleId == null && tidMatch.Success)
            info.TitleId = tidMatch.Groups[1].Value.ToUpperInvariant();
        var verMatch = VersionInName().Match(name);
        if (verMatch.Success && int.TryParse(verMatch.Groups[1].Value, out var v))
            info.Version = v;

        if (info.TitleId != null)
        {
            info.SwitchKind = Classify(info.TitleId);
            info.BaseTitleId = BaseTitleId(info.TitleId);
            info.GameCode = info.BaseTitleId;
        }
        else
        {
            info.SwitchKind = SwitchContentKind.Base;
        }
        return info;
    }

    public static SwitchContentKind Classify(string titleId)
    {
        if (!ulong.TryParse(titleId, System.Globalization.NumberStyles.HexNumber, null, out var tid))
            return SwitchContentKind.Unknown;
        var low = tid & 0xFFF;
        if (low == 0x000)
            return SwitchContentKind.Base;
        if (low == 0x800)
            return SwitchContentKind.Update;
        return SwitchContentKind.Dlc;
    }

    public static string BaseTitleId(string titleId)
    {
        if (!ulong.TryParse(titleId, System.Globalization.NumberStyles.HexNumber, null, out var tid))
            return titleId;
        var kind = Classify(titleId);
        var baseId = kind switch
        {
            SwitchContentKind.Update => tid & ~0xFFFUL,
            SwitchContentKind.Dlc => (tid - 0x1000) & ~0xFFFUL,
            _ => tid,
        };
        return baseId.ToString("X16");
    }

    private static void ReadPfs0(string path, GameFileInfo info)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var header = new byte[16];
        if (fs.Read(header, 0, 16) < 16 || Encoding.ASCII.GetString(header, 0, 4) != "PFS0")
            return;
        var count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        var stringSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
        if (count is <= 0 or > 4096 || stringSize is <= 0 or > 1 << 20)
            return;
        var entries = new byte[count * 0x18];
        fs.ReadExactly(entries);
        var strings = new byte[stringSize];
        fs.ReadExactly(strings);
        for (int i = 0; i < count; i++)
        {
            var nameOffset = BinaryPrimitives.ReadInt32LittleEndian(entries.AsSpan(i * 0x18 + 16));
            if (nameOffset < 0 || nameOffset >= strings.Length)
                continue;
            var end = Array.IndexOf(strings, (byte)0, nameOffset);
            var name = Encoding.ASCII.GetString(strings, nameOffset, (end < 0 ? strings.Length : end) - nameOffset);
            if (name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase) && name.Length >= 16)
                info.TicketTitleIds.Add(name[..16].ToUpperInvariant());
            else if (name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                info.NcaFiles.Add(name);
        }
        if (info.TicketTitleIds.Count > 0)
            info.TitleId = info.TicketTitleIds[0];
    }
}

/// <summary>Liest meta.xml von entpackten Wii-U-Spielen (Cemu-Format).</summary>
public static class WiiUMetaReader
{
    /// <summary>meta.xml-Suffix → Sprachcode.</summary>
    private static readonly (string tag, string code)[] MetaLanguages =
    [
        ("ja", "ja"), ("en", "en"), ("fr", "fr"), ("de", "de"), ("it", "it"), ("es", "es"),
        ("zhs", "zh"), ("ko", "ko"), ("nl", "nl"), ("pt", "pt"), ("ru", "ru"),
    ];

    public static GameFileInfo? ReadFromRpx(string rpxPath)
    {
        var codeDir = System.IO.Path.GetDirectoryName(rpxPath);
        var root = codeDir == null ? null : System.IO.Path.GetDirectoryName(codeDir);
        if (root == null)
            return null;
        var meta = System.IO.Path.Combine(root, "meta", "meta.xml");
        var info = new GameFileInfo { Path = rpxPath, Platform = HubPlatform.WiiU };
        if (!File.Exists(meta))
        {
            info.HeaderTitle = new DirectoryInfo(root).Name;
            return info;
        }
        try
        {
            var doc = XDocument.Load(meta);
            string? Get(string n) => doc.Root?.Element(n)?.Value?.Trim();
            info.HeaderTitle = Get("longname_en")?.Replace('\n', ' ') ?? Get("shortname_en");
            info.GameCode = Get("title_id")?.ToUpperInvariant();
        }
        catch (Exception)
        {
            info.HeaderTitle = new DirectoryInfo(root).Name;
        }
        return info;
    }
}
