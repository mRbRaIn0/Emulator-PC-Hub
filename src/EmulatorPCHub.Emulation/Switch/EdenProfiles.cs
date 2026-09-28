using System.Buffers.Binary;
using System.Text;

namespace EmulatorPCHub.Emulation.Switch;

/// <summary>Ein Switch-Benutzer in Eden. <see cref="Uuid"/> = 32 Hex-Zeichen (UUID::RawString).</summary>
public sealed record EdenUser(int Index, string Uuid, string Name);

/// <summary>
/// Edens Benutzerverwaltung: <c>nand\system\save\8000000000000010\su\avators\profiles.dat</c>
/// (ProfileManager: 0x10 Bytes Kopf, 8 × UserRaw à 0xC8 = UUID, UUID, Zeitstempel, Name (0x20 UTF-8), Daten (0x80)).
/// Spielstände liegen pro Benutzer unter <c>nand\user\save\0000000000000000\&lt;Benutzer&gt;\&lt;TitleID&gt;</c>.
/// </summary>
public static class EdenProfiles
{
    private const int Header = 0x10;
    private const int UserSize = 0xC8;
    private const int MaxUsers = 8;
    private const int NameOffset = 0x28;
    private const int NameSize = 0x20;

    public static string ProfilesFile(string edenDataDir) =>
        Path.Combine(edenDataDir, "nand", "system", "save", "8000000000000010", "su", "avators", "profiles.dat");

    public static IReadOnlyList<EdenUser> List(string edenDataDir)
    {
        var file = ProfilesFile(edenDataDir);
        if (!File.Exists(file))
            return [];
        var data = File.ReadAllBytes(file);
        var result = new List<EdenUser>();
        for (var i = 0; i < MaxUsers; i++)
        {
            var off = Header + i * UserSize;
            if (off + UserSize > data.Length)
                break;
            var uuid = data.AsSpan(off, 16);
            if (uuid.IndexOfAnyExcept((byte)0) < 0)
                continue;
            var nameRaw = data.AsSpan(off + NameOffset, NameSize);
            var end = nameRaw.IndexOf((byte)0);
            var name = Encoding.UTF8.GetString(end >= 0 ? nameRaw[..end] : nameRaw);
            result.Add(new EdenUser(i, Convert.ToHexString(uuid).ToLowerInvariant(), name));
        }
        return result;
    }

    /// <summary>Benennt einen Eden-Benutzer um (z. B. nach dem Mii bzw. Hub-Profil). Eden darf dabei nicht laufen.</summary>
    public static void Rename(string edenDataDir, string uuid, string name)
    {
        var file = ProfilesFile(edenDataDir);
        var data = File.ReadAllBytes(file);
        var user = List(edenDataDir).FirstOrDefault(u => u.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException("Eden-Benutzer nicht gefunden.");
        var span = data.AsSpan(Header + user.Index * UserSize + NameOffset, NameSize);
        span.Clear();
        var bytes = Encoding.UTF8.GetBytes(name);
        var len = Math.Min(bytes.Length, NameSize - 1);
        while (len > 0 && len < bytes.Length && (bytes[len] & 0xC0) == 0x80)
            len--; // kein halbes UTF-8-Zeichen abschneiden
        bytes.AsSpan(0, len).CopyTo(span);
        File.WriteAllBytes(file, data);
    }

    /// <summary>Ordnername der Spielstände eines Benutzers (u128 aus der UUID: obere dann untere 64 Bit, hex).</summary>
    public static string SaveFolderName(string uuid)
    {
        var raw = Convert.FromHexString(uuid);
        var lo = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0, 8));
        var hi = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(8, 8));
        return $"{hi:X16}{lo:X16}";
    }

    /// <summary>Spielstand-Ordner eines Titels für einen Benutzer (neues Layout bevorzugt, wenn vorhanden).</summary>
    public static string SaveDirectory(string edenDataDir, string uuid, string titleId)
    {
        var tid = titleId.ToUpperInvariant();
        var future = Path.Combine(edenDataDir, "nand", "user", "save", "account", uuid.ToLowerInvariant(), tid, "0");
        if (Directory.Exists(future))
            return future;
        return Path.Combine(edenDataDir, "nand", "user", "save", "0000000000000000", SaveFolderName(uuid), tid);
    }
}
