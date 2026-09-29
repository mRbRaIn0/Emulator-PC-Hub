using System.Buffers.Binary;

namespace EmulatorPCHub.Core.Mii;

/// <summary>
/// Wandelt Miis zwischen den Konsolen-Formaten um: Wii (RFL) → Wii U/3DS (Ver3StoreData) → Switch (StoreData).
/// Wii → Wii U übernimmt alle Werte unverändert; nur das Wii-„Gesichtsmerkmal“ wird in Falten und Make-up aufgeteilt
/// und fehlende Streckungen bekommen den Standardwert 3 (wie mii2studio). Wii U → Switch folgt
/// <c>Ver3StoreData::BuildToStoreData</c> aus Eden (Farbtabellen aus <c>raw_data.cpp</c>).
/// </summary>
public static class MiiConvert
{
    public const int SwitchStoreDataSize = 0x44;
    private const int SwitchCoreDataSize = 0x30;

    // Wii-„Gesichtsmerkmal“ (0–11) → Make-up bzw. Falten (Wii U/Switch)
    private static readonly Dictionary<int, int> WiiMakeup = new() { [1] = 1, [2] = 6, [3] = 9, [9] = 10 };
    private static readonly Dictionary<int, int> WiiWrinkles = new() { [4] = 5, [5] = 2, [6] = 3, [7] = 7, [8] = 8, [10] = 9, [11] = 11 };

    // Wii-U-Farbe → Switch-Farbe (Eden RawData::Ver3…ColorTable)
    private static readonly byte[] Ver3HairColor = [0x8, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7];
    private static readonly byte[] Ver3EyeColor = [0x8, 0x9, 0xa, 0xb, 0xc, 0xd];
    private static readonly byte[] Ver3MouthColor = [0x13, 0x14, 0x15, 0x16, 0x17];
    private static readonly byte[] Ver3GlassColor = [0x8, 0xe, 0xf, 0x10, 0x11, 0x12, 0x0];

    /// <summary>Wii-Mii (74 Bytes) → Wii-U-/3DS-Mii (96 Bytes, mit Prüfsumme).</summary>
    public static byte[] WiiToVer3(ReadOnlySpan<byte> wii, DateTime? created = null)
    {
        if (wii.Length != MiiCodec.WiiSize)
            throw new InvalidDataException("Kein Wii-Mii (74 Bytes).");
        var info = BinaryPrimitives.ReadUInt16BigEndian(wii);
        var face = BinaryPrimitives.ReadUInt16BigEndian(wii[0x20..]);
        var hair = BinaryPrimitives.ReadUInt16BigEndian(wii[0x22..]);
        var brow = BinaryPrimitives.ReadUInt32BigEndian(wii[0x24..]);
        var eye = BinaryPrimitives.ReadUInt32BigEndian(wii[0x28..]);
        var nose = BinaryPrimitives.ReadUInt16BigEndian(wii[0x2C..]);
        var mouth = BinaryPrimitives.ReadUInt16BigEndian(wii[0x2E..]);
        var glass = BinaryPrimitives.ReadUInt16BigEndian(wii[0x30..]);
        var beard = BinaryPrimitives.ReadUInt16BigEndian(wii[0x32..]);
        var mole = BinaryPrimitives.ReadUInt16BigEndian(wii[0x34..]);
        static int B(uint v, int shift, int bits) => (int)(v >> shift & ((1u << bits) - 1));

        var v = new byte[MiiCodec.Ver3Size];
        v[0x00] = 3;    // Version
        v[0x01] = 0x01; // Kopieren erlaubt, Schriftregion Standard
        // Mii-ID aus Wii-Mii-ID + Konsolen-ID übernehmen, Erstellzeit in Sekunden/2 seit 2010, Bit 31 = normales Mii
        wii.Slice(0x18, 2).CopyTo(v.AsSpan(0x02));
        wii.Slice(0x1C, 4).CopyTo(v.AsSpan(0x04));
        wii.Slice(0x18, 4).CopyTo(v.AsSpan(0x08));
        var seconds = (uint)Math.Max(0, ((created ?? DateTime.UtcNow) - new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds / 2);
        BinaryPrimitives.WriteUInt32BigEndian(v.AsSpan(0x0C), 0x80000000u | seconds & 0x0FFFFFFF);
        wii.Slice(0x1C, 4).CopyTo(v.AsSpan(0x10)); // Ersteller-MAC (nur Kennung)

        var gender = B(info, 14, 1);
        var month = B(info, 10, 4);
        var day = B(info, 5, 5);
        var favorite = Math.Min(B(info, 1, 4), 11);
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x18), (ushort)(gender | month << 1 | day << 5 | favorite << 10));
        CopyName(wii.Slice(0x02, 20), v.AsSpan(0x1A, 20));
        v[0x2E] = (byte)Math.Min((int)wii[0x16], 127);
        v[0x2F] = (byte)Math.Min((int)wii[0x17], 127);

        var feature = B(face, 6, 4);
        v[0x30] = (byte)(B(face, 13, 3) << 1 | B(face, 10, 3) << 5);
        v[0x31] = (byte)(WiiWrinkles.GetValueOrDefault(feature) | WiiMakeup.GetValueOrDefault(feature) << 4);
        v[0x32] = (byte)B(hair, 9, 7);
        v[0x33] = (byte)(B(hair, 6, 3) | B(hair, 5, 1) << 3);
        BinaryPrimitives.WriteUInt32LittleEndian(v.AsSpan(0x34), (uint)(
            B(eye, 26, 6) | B(eye, 13, 3) << 6 | B(eye, 9, 3) << 9 | 3 << 13 | B(eye, 21, 3) << 16 | B(eye, 5, 4) << 21 | B(eye, 16, 5) << 25));
        BinaryPrimitives.WriteUInt32LittleEndian(v.AsSpan(0x38), (uint)(
            B(brow, 27, 5) | B(brow, 13, 3) << 5 | B(brow, 9, 4) << 8 | 3 << 12 | B(brow, 22, 4) << 16 | B(brow, 0, 4) << 21 | B(brow, 4, 5) << 25));
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x3C), (ushort)(B(nose, 12, 4) | B(nose, 8, 4) << 5 | B(nose, 3, 5) << 9));
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x3E), (ushort)(B(mouth, 11, 5) | B(mouth, 9, 2) << 6 | B(mouth, 5, 4) << 9 | 3 << 13));
        v[0x40] = (byte)(B(mouth, 0, 5) | B(beard, 14, 2) << 5);
        v[0x41] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x42), (ushort)(B(beard, 12, 2) | B(beard, 9, 3) << 3 | B(beard, 5, 4) << 6 | B(beard, 0, 5) << 10));
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x44), (ushort)(B(glass, 12, 4) | B(glass, 9, 3) << 4 | B(glass, 5, 3) << 7 | B(glass, 0, 5) << 11));
        BinaryPrimitives.WriteUInt16LittleEndian(v.AsSpan(0x46), (ushort)(B(mole, 15, 1) | B(mole, 11, 4) << 1 | B(mole, 1, 5) << 5 | B(mole, 6, 5) << 10));
        CopyName(wii.Slice(0x36, 20), v.AsSpan(0x48, 20));
        MiiCodec.FixVer3Crc(v);
        return v;
    }

    /// <summary>Wii-, Wii-U- oder 3DS-Mii → Switch-StoreData (68 Bytes) mit neuer Erstell-ID und Prüfsummen.</summary>
    public static byte[] ToSwitchStoreData(ReadOnlySpan<byte> data, Guid? createId = null) =>
        MiiCodec.DetectFormat(data) switch
        {
            MiiFormat.Wii => Ver3ToSwitchStoreData(WiiToVer3(data), createId),
            MiiFormat.Ver3 => Ver3ToSwitchStoreData(data, createId),
            _ => throw new InvalidDataException("Unbekanntes Mii-Format."),
        };

    /// <summary>Wii-U-/3DS-Mii → Switch-StoreData (wie Eden <c>Ver3StoreData::BuildToStoreData</c>).</summary>
    public static byte[] Ver3ToSwitchStoreData(ReadOnlySpan<byte> v, Guid? createId = null)
    {
        if (v.Length != MiiCodec.Ver3Size)
            throw new InvalidDataException("Kein Wii-U-/3DS-Mii (96 Bytes).");
        var info = BinaryPrimitives.ReadUInt16LittleEndian(v[0x18..]);
        var eye = BinaryPrimitives.ReadUInt32LittleEndian(v[0x34..]);
        var brow = BinaryPrimitives.ReadUInt32LittleEndian(v[0x38..]);
        var nose = BinaryPrimitives.ReadUInt16LittleEndian(v[0x3C..]);
        var mouth = BinaryPrimitives.ReadUInt16LittleEndian(v[0x3E..]);
        var beard = BinaryPrimitives.ReadUInt16LittleEndian(v[0x42..]);
        var glass = BinaryPrimitives.ReadUInt16LittleEndian(v[0x44..]);
        var mole = BinaryPrimitives.ReadUInt16LittleEndian(v[0x46..]);
        static uint B(uint value, int shift, int bits) => value >> shift & ((1u << bits) - 1);
        static uint T(byte[] table, uint index) => table[Math.Min(index, (uint)table.Length - 1)];

        uint gender = B(info, 0, 1), favorite = B(info, 10, 4);
        uint height = v[0x2E], build = v[0x2F];
        uint facelineType = B(v[0x30], 1, 4), facelineColor = Math.Min(B(v[0x30], 5, 3), 5u);
        uint wrinkle = B(v[0x31], 0, 4), make = B(v[0x31], 4, 4);
        uint hairType = v[0x32], hairColor = T(Ver3HairColor, B(v[0x33], 0, 3)), hairFlip = B(v[0x33], 3, 1);
        uint eyeType = B(eye, 0, 6), eyeColor = T(Ver3EyeColor, B(eye, 6, 3)), eyeScale = B(eye, 9, 4), eyeAspect = B(eye, 13, 3),
            eyeRotate = B(eye, 16, 5), eyeX = B(eye, 21, 4), eyeY = B(eye, 25, 5);
        uint browType = B(brow, 0, 5), browColor = T(Ver3HairColor, B(brow, 5, 3)), browScale = B(brow, 8, 4), browAspect = B(brow, 12, 3),
            browRotate = B(brow, 16, 4), browX = B(brow, 21, 4), browY = (uint)Math.Max(0, (int)B(brow, 25, 5) - 3);
        uint noseType = B(nose, 0, 5), noseScale = B(nose, 5, 4), noseY = B(nose, 9, 5);
        uint mouthType = B(mouth, 0, 6), mouthColor = T(Ver3MouthColor, B(mouth, 6, 3)), mouthScale = B(mouth, 9, 4), mouthAspect = B(mouth, 13, 3);
        uint mouthY = B(v[0x40], 0, 5), mustacheType = B(v[0x40], 5, 3);
        uint beardType = B(beard, 0, 3), beardColor = T(Ver3HairColor, B(beard, 3, 3)), mustacheScale = B(beard, 6, 4), mustacheY = B(beard, 10, 5);
        uint glassType = B(glass, 0, 4), glassColor = T(Ver3GlassColor, B(glass, 4, 3)), glassScale = B(glass, 7, 4), glassY = B(glass, 11, 5);
        uint moleType = B(mole, 0, 1), moleScale = B(mole, 1, 4), moleX = B(mole, 5, 5), moleY = B(mole, 10, 5);
        const uint type = 0, regionMove = 0, fontRegion = 0;

        var s = new byte[SwitchStoreDataSize];
        Span<uint> w =
        [
            hairType | height << 8 | moleType << 15 | build << 16 | hairFlip << 23 | hairColor << 24 | type << 31,
            eyeColor | gender << 7 | browColor << 8 | mouthColor << 16 | beardColor << 24,
            glassColor | eyeType << 8 | regionMove << 14 | mouthType << 16 | fontRegion << 22 | eyeY << 24 | Math.Min(glassScale, 7u) << 29,
            browType | mustacheType << 5 | noseType << 8 | beardType << 13 | noseY << 16 | mouthAspect << 21 | mouthY << 24 | browAspect << 29,
            mustacheY | Math.Min(eyeRotate, 7u) << 5 | glassY << 8 | eyeAspect << 13 | moleX << 16 | Math.Min(eyeScale, 7u) << 21 | moleY << 24,
            glassType | favorite << 8 | facelineType << 12 | facelineColor << 16 | wrinkle << 20 | make << 24 | eyeX << 28,
            browScale | browRotate << 4 | browX << 8 | browY << 12 | noseScale << 16 | mouthScale << 20 | mustacheScale << 24 | moleScale << 28,
        ];
        for (var i = 0; i < w.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(i * 4), w[i]);
        v.Slice(0x1A, 20).CopyTo(s.AsSpan(0x1C)); // Name (UTF-16 LE, 10 Zeichen)

        (createId ?? Guid.NewGuid()).TryWriteBytes(s.AsSpan(0x30, 16), bigEndian: true, out _);
        SetSwitchChecksums(s);
        return s;
    }

    /// <summary>
    /// Feste Switch-Erstell-ID für ein Wii-Mii (aus Wii-Mii-ID + Konsolen-ID): So wird ein im Mii-Kanal geändertes Mii
    /// beim nächsten Abgleich in Eden aktualisiert statt doppelt angelegt.
    /// </summary>
    public static Guid StableCreateId(ReadOnlySpan<byte> wii)
    {
        Span<byte> seed = stackalloc byte[16];
        "hubmii01"u8.CopyTo(seed);
        wii.Slice(0x18, 8).CopyTo(seed[8..]);
        var hash = System.Security.Cryptography.MD5.HashData(seed);
        hash[6] = (byte)(hash[6] & 0x0F | 0x40); // RFC 4122 Version 4
        hash[8] = (byte)(hash[8] & 0x3F | 0x80);
        return new Guid(hash, bigEndian: true);
    }

    /// <summary>Feste Switch-Erstell-ID für ein Wii-U-/3DS-Mii (aus dessen Mii-ID und Erstelldatum).</summary>
    public static Guid StableCreateIdVer3(ReadOnlySpan<byte> ver3)
    {
        Span<byte> seed = stackalloc byte[20];
        "hubmii03"u8.CopyTo(seed);
        ver3.Slice(0x04, 12).CopyTo(seed[8..]);
        var hash = System.Security.Cryptography.MD5.HashData(seed);
        hash[6] = (byte)(hash[6] & 0x0F | 0x40);
        hash[8] = (byte)(hash[8] & 0x3F | 0x80);
        return new Guid(hash, bigEndian: true);
    }

    /// <summary>Datenprüfsumme (CoreData + Erstell-ID) und Geräteprüfsumme wie Eden <c>StoreData::SetChecksum</c>.</summary>
    public static void SetSwitchChecksums(Span<byte> storeData)
    {
        BinaryPrimitives.WriteUInt16BigEndian(storeData[0x40..], MiiCodec.Crc16(storeData[..0x40]));
        BinaryPrimitives.WriteUInt16BigEndian(storeData[0x42..], SwitchDeviceCrc(SwitchStoreDataSize));
    }

    /// <summary>Switch-Mii-Name aus StoreData.</summary>
    public static string SwitchName(ReadOnlySpan<byte> storeData)
    {
        var chars = new List<char>();
        for (var i = 0; i < 10; i++)
        {
            var c = (char)BinaryPrimitives.ReadUInt16LittleEndian(storeData[(0x1C + i * 2)..]);
            if (c == 0)
                break;
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }

    /// <summary>Nur das Aussehen + Name (CoreData) – zum Erkennen doppelter Miis.</summary>
    public static ReadOnlySpan<byte> SwitchCoreData(ReadOnlySpan<byte> storeData) => storeData[..SwitchCoreDataSize];

    // Eden MiiUtil::CalculateDeviceCrc16 mit der Geräte-ID „Eden Default UID“ (Eden berechnet sie beim Laden ohnehin neu).
    private static ushort SwitchDeviceCrc(int dataSize)
    {
        const int magic = 0x1021;
        var crc = 0;
        foreach (var b in "Eden Default UID"u8)
        {
            for (var j = 0; j < 8; j++)
            {
                crc = crc << 1 & 0x1FFFF;
                if ((crc & 0x10000) != 0)
                    crc = (crc ^ magic) & 0xFFFF;
            }
            crc ^= b;
        }
        for (var i = 0; i < dataSize * 8; i++)
        {
            crc = crc << 1 & 0x1FFFF;
            if ((crc & 0x10000) != 0)
                crc = (crc ^ magic) & 0xFFFF;
        }
        return (ushort)crc;
    }

    private static void CopyName(ReadOnlySpan<byte> bigEndianUtf16, Span<byte> littleEndianUtf16)
    {
        for (var i = 0; i + 1 < bigEndianUtf16.Length; i += 2)
        {
            littleEndianUtf16[i] = bigEndianUtf16[i + 1];
            littleEndianUtf16[i + 1] = bigEndianUtf16[i];
        }
    }
}
