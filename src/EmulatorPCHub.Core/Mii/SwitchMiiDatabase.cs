using System.Buffers.Binary;

namespace EmulatorPCHub.Core.Mii;

/// <summary>
/// Switch-Mii-Datenbank („NFDB“), wie Eden sie unter <c>nand\system\save\8000000000000030\MiiDatabase.dat</c> speichert
/// (Eden <c>NintendoFigurineDatabase</c>): Magic, 100 × StoreData (0x44), Version 1, Anzahl, CRC-16 (Big Endian).
/// Spiele wie Tomodachi Life lesen ihre Miis von hier.
/// </summary>
public sealed class SwitchMiiDatabase
{
    public const int MaxMiis = 100;
    public const int FileSize = 0x1A98;
    private const int MiisOffset = 4;
    private const int VersionOffset = MiisOffset + MaxMiis * MiiConvert.SwitchStoreDataSize; // 0x1A94
    private const int CountOffset = VersionOffset + 1;
    private const int CrcOffset = VersionOffset + 2;

    private readonly byte[] _data;

    public string FilePath { get; }

    private SwitchMiiDatabase(string path, byte[] data)
    {
        FilePath = path;
        _data = data;
    }

    public static string PathIn(string edenDataDir) =>
        Path.Combine(edenDataDir, "nand", "system", "save", "8000000000000030", "MiiDatabase.dat");

    /// <summary>Lädt die Datenbank oder legt eine leere an, wenn sie fehlt oder ungültig ist (wie Eden selbst).</summary>
    public static SwitchMiiDatabase LoadOrCreate(string path)
    {
        if (File.Exists(path))
        {
            var data = File.ReadAllBytes(path);
            if (data.Length == FileSize && "NFDB"u8.SequenceEqual(data.AsSpan(0, 4)) && data[VersionOffset] == 1
                && MiiCodec.Crc16(data.AsSpan(0, CrcOffset)) == BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(CrcOffset)))
                return new SwitchMiiDatabase(path, data);
        }
        var empty = new byte[FileSize];
        "NFDB"u8.CopyTo(empty);
        empty[VersionOffset] = 1;
        return new SwitchMiiDatabase(path, empty);
    }

    public int Count => _data[CountOffset];

    public byte[] Get(int index) => _data.AsSpan(MiisOffset + index * MiiConvert.SwitchStoreDataSize, MiiConvert.SwitchStoreDataSize).ToArray();

    public IReadOnlyList<string> Names => Enumerable.Range(0, Count).Select(i => MiiConvert.SwitchName(Get(i))).ToList();

    /// <summary>Fügt ein Mii hinzu. Rückgabe: Platz (0-basiert), -1 bei voller Datenbank; gleiches Aussehen + Name wird ersetzt.</summary>
    public int Add(ReadOnlySpan<byte> storeData)
    {
        if (storeData.Length != MiiConvert.SwitchStoreDataSize)
            throw new ArgumentException("Switch-StoreData muss 0x44 Bytes lang sein.", nameof(storeData));
        var core = MiiConvert.SwitchCoreData(storeData);
        for (var i = 0; i < Count; i++)
        {
            if (MiiConvert.SwitchCoreData(Get(i)).SequenceEqual(core))
                return i; // schon vorhanden
        }
        if (Count >= MaxMiis - 1) // Eden hält eine Datenbank mit 100 Einträgen für defekt
            return -1;
        var slot = Count;
        storeData.CopyTo(_data.AsSpan(MiisOffset + slot * MiiConvert.SwitchStoreDataSize));
        _data[CountOffset] = (byte)(slot + 1);
        return slot;
    }

    /// <summary>
    /// Mii eintragen oder aktualisieren: gleiche Erstell-ID (bzw. gleiches Aussehen) → Platz überschreiben, sonst anhängen.
    /// Rückgabe: Platz, -1 bei voller Datenbank. <paramref name="changed"/> = ob sich etwas geändert hat.
    /// </summary>
    public int Upsert(ReadOnlySpan<byte> storeData, out bool changed)
    {
        changed = false;
        var createId = storeData.Slice(0x30, 16);
        var core = MiiConvert.SwitchCoreData(storeData);
        for (var i = 0; i < Count; i++)
        {
            var existing = Get(i);
            if (!existing.AsSpan(0x30, 16).SequenceEqual(createId) && !MiiConvert.SwitchCoreData(existing).SequenceEqual(core))
                continue;
            if (!existing.AsSpan().SequenceEqual(storeData))
            {
                storeData.CopyTo(_data.AsSpan(MiisOffset + i * MiiConvert.SwitchStoreDataSize));
                changed = true;
            }
            return i;
        }
        var slot = Add(storeData);
        changed = slot >= 0;
        return slot;
    }

    public void Save()
    {
        BinaryPrimitives.WriteUInt16BigEndian(_data.AsSpan(CrcOffset), MiiCodec.Crc16(_data.AsSpan(0, CrcOffset)));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllBytes(FilePath, _data);
    }
}
