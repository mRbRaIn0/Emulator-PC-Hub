using System.Buffers.Binary;
using System.Text;
using EmulatorPCHub.Core.Backup;

namespace EmulatorPCHub.Emulation.Dolphin;

/// <summary>
/// Wii-Systemeinstellungen (NAND <c>shared2/sys/SYSCONF</c>) in Dolphins Userordner lesen und einzelne Werte ändern.
/// Format: „SCv0“, Anzahl (u16 BE), Offset-Tabelle, Einträge (Typ&lt;&lt;5 | Namenslänge−1, Name, Daten), „SCed“ am Ende.
/// Es werden nur vorhandene BYTE/LONG-Einträge an Ort und Stelle überschrieben – die Dateistruktur bleibt unverändert.
/// </summary>
public sealed class WiiSysconf
{
    private const int TypeByte = 3;
    private const int TypeLong = 5;

    /// <summary>Position der Sensorleiste: 0 = unter dem Bildschirm, 1 = darüber (Dolphin: „Top“ = 1).</summary>
    public const string SensorBarPosition = "BT.BAR";
    /// <summary>IR-Empfindlichkeit 1–5 (Standard 3).</summary>
    public const string SensorBarSensitivity = "BT.SENS";
    /// <summary>Lautsprecher der Wii Remote 0–127.</summary>
    public const string SpeakerVolume = "BT.SPKV";
    /// <summary>Vibration der Wii Remote an/aus.</summary>
    public const string WiimoteMotor = "BT.MOT";

    private readonly string _path;
    private byte[] _data;

    private WiiSysconf(string path, byte[] data)
    {
        _path = path;
        _data = data;
    }

    public static string PathIn(string dolphinUserDir) => Path.Combine(dolphinUserDir, "Wii", "shared2", "sys", "SYSCONF");

    /// <summary>Lädt die Datei; null, wenn sie fehlt oder kein gültiges SYSCONF ist (Dolphin legt sie beim ersten Wii-Start an).</summary>
    public static WiiSysconf? Load(string path)
    {
        if (!File.Exists(path))
            return null;
        var data = File.ReadAllBytes(path);
        return IsValid(data) ? new WiiSysconf(path, data) : null;
    }

    public static WiiSysconf? FromBytes(byte[] data, string path = "") => IsValid(data) ? new WiiSysconf(path, data) : null;

    public byte[] Bytes => _data;

    private static bool IsValid(byte[] d) =>
        d.Length >= 16 && d.AsSpan(0, 4).SequenceEqual("SCv0"u8) && d.AsSpan(d.Length - 4).SequenceEqual("SCed"u8);

    /// <summary>Datenposition und Typ eines Eintrags.</summary>
    private (int Offset, int Type)? Find(string name)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(4));
        for (var i = 0; i < count; i++)
        {
            var entry = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(6 + 2 * i));
            if (entry + 1 >= _data.Length)
                continue;
            var type = _data[entry] >> 5;
            var nameLen = (_data[entry] & 0x1F) + 1;
            if (entry + 1 + nameLen >= _data.Length)
                continue;
            if (Encoding.ASCII.GetString(_data, entry + 1, nameLen) == name)
                return (entry + 1 + nameLen, type);
        }
        return null;
    }

    public int? Get(string name)
    {
        if (Find(name) is not { } e)
            return null;
        return e.Type switch
        {
            TypeByte => _data[e.Offset],
            TypeLong when e.Offset + 4 <= _data.Length => (int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(e.Offset)),
            _ => null,
        };
    }

    /// <summary>Wert setzen – false, wenn der Eintrag fehlt oder keinen passenden Typ hat.</summary>
    public bool Set(string name, int value)
    {
        if (Find(name) is not { } e)
            return false;
        switch (e.Type)
        {
            case TypeByte:
                _data[e.Offset] = (byte)Math.Clamp(value, 0, 255);
                return true;
            case TypeLong when e.Offset + 4 <= _data.Length:
                BinaryPrimitives.WriteUInt32BigEndian(_data.AsSpan(e.Offset), (uint)Math.Max(0, value));
                return true;
            default:
                return false;
        }
    }

    /// <summary>Speichern (Original einmal sichern). Dolphin darf dabei nicht laufen, sonst überschreibt es die Datei beim Beenden.</summary>
    public void Save(BackupService? backups)
    {
        if (backups != null && !File.Exists(_path + ".hub-original"))
        {
            File.Copy(_path, _path + ".hub-original");
            backups.BackupFile(BackupCategory.Config, _path, "dolphin-SYSCONF");
        }
        var tmp = _path + ".tmp";
        File.WriteAllBytes(tmp, _data);
        File.Move(tmp, _path, overwrite: true);
    }
}
