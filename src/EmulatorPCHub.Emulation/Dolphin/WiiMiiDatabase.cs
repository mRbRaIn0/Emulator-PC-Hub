using System.Buffers.Binary;
using EmulatorPCHub.Core.Mii;

namespace EmulatorPCHub.Emulation.Dolphin;

/// <summary>
/// Mii-Datenbank der (emulierten) Wii: <c>Wii\shared2\menu\FaceLib\RFL_DB.dat</c> in Dolphins NAND.
/// Aufbau (WiiBrew): „RNOD“, 100 Miis à 0x4A Bytes, ab 0x1D00 Mii-Parade („RNHD“),
/// CRC-16 der ersten 0x1F1DE Bytes bei 0x1F1DE.
/// </summary>
public sealed class WiiMiiDatabase
{
    public const int Slots = 100;
    private const int FirstSlot = 4;
    private const int CrcOffset = 0x1F1DE;
    private static readonly byte[] Magic = "RNOD"u8.ToArray();

    private readonly byte[] _data;

    public string FilePath { get; }

    private WiiMiiDatabase(string file, byte[] data)
    {
        FilePath = file;
        _data = data;
    }

    public static string PathIn(string dolphinUserDir) =>
        Path.Combine(dolphinUserDir, "Wii", "shared2", "menu", "FaceLib", "RFL_DB.dat");

    /// <summary>Lädt die Datenbank; null, wenn sie fehlt (Mii-Kanal bzw. ein Mii-Spiel wurde noch nie gestartet).</summary>
    public static WiiMiiDatabase? Load(string file)
    {
        if (!File.Exists(file))
            return null;
        var data = File.ReadAllBytes(file);
        if (data.Length < CrcOffset + 2 || !data.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("RFL_DB.dat hat kein gültiges Format.");
        return new WiiMiiDatabase(file, data);
    }

    private Span<byte> Slot(int i) => _data.AsSpan(FirstSlot + i * MiiCodec.WiiSize, MiiCodec.WiiSize);

    /// <summary>Alle belegten Plätze mit Rohdaten.</summary>
    public IEnumerable<(int Slot, byte[] Data)> Miis()
    {
        for (var i = 0; i < Slots; i++)
        {
            var raw = Slot(i).ToArray();
            if (MiiCodec.Read(raw) != null)
                yield return (i, raw);
        }
    }

    /// <summary>
    /// Speichert ein Wii-Mii: ersetzt ein Mii mit derselben Mii-ID, sonst ersten freien Platz.
    /// Gibt den Platz zurück (oder -1, wenn alle 100 Plätze belegt sind).
    /// </summary>
    public int Put(ReadOnlySpan<byte> mii)
    {
        if (mii.Length != MiiCodec.WiiSize)
            throw new ArgumentException("Nur Wii-Miis (74 Bytes) können in die Wii-Datenbank.");
        var id = mii.Slice(0x18, 4);
        var target = -1;
        for (var i = 0; i < Slots && target < 0; i++)
            if (MiiCodec.Read(Slot(i)) != null && Slot(i).Slice(0x18, 4).SequenceEqual(id))
                target = i;
        for (var i = 0; i < Slots && target < 0; i++)
            if (MiiCodec.Read(Slot(i)) == null)
                target = i;
        if (target < 0)
            return -1;
        mii.CopyTo(Slot(target));
        return target;
    }

    public void Save()
    {
        var crc = MiiCodec.Crc16(_data.AsSpan(0, CrcOffset));
        BinaryPrimitives.WriteUInt16BigEndian(_data.AsSpan(CrcOffset), crc);
        File.WriteAllBytes(FilePath, _data);
    }

    public bool HasValidCrc =>
        MiiCodec.Crc16(_data.AsSpan(0, CrcOffset)) == BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(CrcOffset));
}
