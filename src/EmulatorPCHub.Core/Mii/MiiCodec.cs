using System.Buffers.Binary;
using System.Text;

namespace EmulatorPCHub.Core.Mii;

/// <summary>Natives Mii-Format.</summary>
public enum MiiFormat
{
    /// <summary>Wii (RFL, 74 Bytes, Big Endian) – Mii-Kanal, Dolphin <c>RFL_DB.dat</c>, <c>.mii</c>-Dateien.</summary>
    Wii,
    /// <summary>Wii U / 3DS (FFLStoreData bzw. Ver3StoreData, 96 Bytes mit CRC) – Cemu <c>account.dat</c>, <c>.ffsd</c>.</summary>
    Ver3,
}

/// <summary>Die Felder, die der Hub aus einem Mii liest (Aussehen bleibt unverändert in den Rohdaten).</summary>
public sealed record MiiInfo(
    MiiFormat Format,
    string Name,
    string Creator,
    bool IsGirl,
    int FavoriteColor,
    int BirthMonth,
    int BirthDay,
    string MiiId);

/// <summary>
/// Liest und schreibt Mii-Rohdaten. Quellen: WiiBrew „Mii data“ (Wii) sowie Eden <c>ver3_store_data.h</c> und
/// Cemu <c>Account.cpp</c> (Wii U). Prüfsummen sind CRC-16/CCITT (Polynom 0x1021, Start 0, „XMODEM“).
/// </summary>
public static class MiiCodec
{
    public const int WiiSize = 0x4A;
    public const int Ver3Size = 0x60;
    private const int NameChars = 10;

    /// <summary>Lieblingsfarben 0–11 (Rot … Schwarz) als Hex für die Anzeige.</summary>
    public static IReadOnlyList<string> FavoriteColors { get; } =
    [
        "#FFD21E14", "#FFFF6E19", "#FFFFD820", "#FF78D220", "#FF007830", "#FF0A48B4",
        "#FF3CAADE", "#FFF55A7D", "#FF7328AD", "#FF483818", "#FFE0E0E0", "#FF181814",
    ];

    public static IReadOnlyList<string> FavoriteColorNames { get; } =
        ["Rot", "Orange", "Gelb", "Hellgrün", "Grün", "Blau", "Hellblau", "Pink", "Lila", "Braun", "Weiß", "Schwarz"];

    public static MiiFormat? DetectFormat(ReadOnlySpan<byte> data) => data.Length switch
    {
        WiiSize => MiiFormat.Wii,
        Ver3Size => MiiFormat.Ver3,
        _ => null,
    };

    /// <summary>Liest ein Mii; null, wenn die Daten leer oder kein gültiges Mii sind.</summary>
    public static MiiInfo? Read(ReadOnlySpan<byte> data)
    {
        var format = DetectFormat(data);
        if (format == null || data.IndexOfAnyExcept((byte)0) < 0)
            return null;
        if (format == MiiFormat.Wii)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(data);
            var name = ReadName(data.Slice(0x02, NameChars * 2), bigEndian: true);
            if (name.Length == 0)
                return null;
            return new MiiInfo(MiiFormat.Wii, name,
                ReadName(data.Slice(0x36, NameChars * 2), bigEndian: true),
                (flags >> 14 & 1) == 1,
                Math.Min(flags >> 1 & 0xF, 11),
                flags >> 10 & 0xF,
                flags >> 5 & 0x1F,
                Convert.ToHexString(data.Slice(0x18, 4)));
        }
        else
        {
            var bits = BinaryPrimitives.ReadUInt16LittleEndian(data[0x18..]);
            var name = ReadName(data.Slice(0x1A, NameChars * 2), bigEndian: false);
            if (name.Length == 0)
                return null;
            return new MiiInfo(MiiFormat.Ver3, name,
                ReadName(data.Slice(0x48, NameChars * 2), bigEndian: false),
                (bits & 1) == 1,
                Math.Min(bits >> 10 & 0xF, 11),
                bits >> 1 & 0xF,
                bits >> 5 & 0x1F,
                Convert.ToHexString(data.Slice(0x0C, 4)));
        }
    }

    /// <summary>Ändert den Namen (max. 10 Zeichen) und aktualisiert bei Wii U die Prüfsumme.</summary>
    public static byte[] WithName(ReadOnlySpan<byte> data, string name)
    {
        var copy = data.ToArray();
        var format = DetectFormat(copy) ?? throw new InvalidDataException("Unbekanntes Mii-Format.");
        var span = format == MiiFormat.Wii ? copy.AsSpan(0x02, NameChars * 2) : copy.AsSpan(0x1A, NameChars * 2);
        span.Clear();
        var chars = name.Length > NameChars ? name[..NameChars] : name;
        for (var i = 0; i < chars.Length; i++)
        {
            if (format == MiiFormat.Wii)
                BinaryPrimitives.WriteUInt16BigEndian(span[(i * 2)..], chars[i]);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(span[(i * 2)..], chars[i]);
        }
        if (format == MiiFormat.Ver3)
            FixVer3Crc(copy);
        return copy;
    }

    /// <summary>Prüfsumme eines Wii-U-Mii (letzte 2 Bytes, Big Endian) neu berechnen.</summary>
    public static void FixVer3Crc(Span<byte> data)
    {
        var crc = Crc16(data[..(Ver3Size - 2)]);
        BinaryPrimitives.WriteUInt16BigEndian(data[(Ver3Size - 2)..], crc);
    }

    public static bool HasValidVer3Crc(ReadOnlySpan<byte> data) =>
        data.Length == Ver3Size && Crc16(data[..(Ver3Size - 2)]) == BinaryPrimitives.ReadUInt16BigEndian(data[(Ver3Size - 2)..]);

    /// <summary>CRC-16/CCITT (XMODEM): Polynom 0x1021, Startwert 0.</summary>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)(crc << 1 ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    private static string ReadName(ReadOnlySpan<byte> raw, bool bigEndian)
    {
        var sb = new StringBuilder();
        for (var i = 0; i + 1 < raw.Length; i += 2)
        {
            var c = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(raw[i..]) : BinaryPrimitives.ReadUInt16LittleEndian(raw[i..]);
            if (c == 0)
                break;
            sb.Append((char)c);
        }
        return sb.ToString().Trim();
    }

    public static string FileExtension(MiiFormat format) => format == MiiFormat.Wii ? ".mii" : ".ffsd";

    public static string PlatformName(MiiFormat format) => format == MiiFormat.Wii ? "Wii" : "Wii U / 3DS";
}
