using System.Globalization;
using System.Text;

namespace EmulatorPCHub.Emulation.Cemu;

/// <summary>Ein Wii-U-Konto in Cemu (<c>mlc01\usr\save\system\act\&lt;PersistentId&gt;\account.dat</c>).</summary>
public sealed record CemuAccount(string PersistentId, string MiiName, byte[] MiiData, string FilePath);

/// <summary>
/// Liest/schreibt Cemus Konten (Format wie Cemu <c>Account::Save</c>: Textdatei „Schlüssel=Wert“,
/// <c>MiiData</c> = 96 Bytes als Hex, <c>MiiName</c> = 11 UTF-16-Zeichen als je 4 Hex-Ziffern).
/// Cemu trennt Spielstände über diese Konten (<c>usr\save\…\user\&lt;PersistentId&gt;</c>).
/// </summary>
public static class CemuAccounts
{
    public static string ActDirectory(string mlc) => Path.Combine(mlc, "usr", "save", "system", "act");

    public static IReadOnlyList<CemuAccount> List(string mlc)
    {
        var dir = ActDirectory(mlc);
        if (!Directory.Exists(dir))
            return [];
        var result = new List<CemuAccount>();
        foreach (var sub in Directory.EnumerateDirectories(dir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var file = Path.Combine(sub, "account.dat");
            if (File.Exists(file) && Read(file) is { } acc)
                result.Add(acc);
        }
        return result;
    }

    public static CemuAccount? Read(string file)
    {
        var values = File.ReadAllLines(file)
            .Select(l => l.Split('=', 2))
            .Where(p => p.Length == 2)
            .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);
        if (!values.TryGetValue("PersistentId", out var id))
            return null;
        byte[] mii = [];
        if (values.TryGetValue("MiiData", out var hex) && hex.Length == 192)
            mii = Convert.FromHexString(hex);
        return new CemuAccount(id, DecodeName(values.GetValueOrDefault("MiiName", "")), mii, file);
    }

    private static string DecodeName(string hex)
    {
        var sb = new StringBuilder();
        for (var i = 0; i + 4 <= hex.Length; i += 4)
        {
            var c = ushort.Parse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (c == 0)
                break;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    private static string EncodeName(string name)
    {
        var chars = (name.Length > 10 ? name[..10] : name).PadRight(11, '\0');
        return string.Concat(chars.Select(c => ((ushort)c).ToString("x4", CultureInfo.InvariantCulture)));
    }

    /// <summary>Setzt Mii (96 Bytes, Wii-U-Format) und Mii-Namen eines Kontos – alle anderen Zeilen bleiben erhalten.</summary>
    public static void SetMii(string file, byte[] miiData, string name)
    {
        if (miiData.Length != 96)
            throw new ArgumentException("Cemu-Konten brauchen ein Wii-U-Mii (96 Bytes).");
        var lines = File.ReadAllLines(file).ToList();
        void Put(string key, string value)
        {
            var i = lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
            if (i >= 0)
                lines[i] = $"{key}={value}";
            else
                lines.Add($"{key}={value}");
        }
        Put("MiiData", Convert.ToHexString(miiData).ToLowerInvariant());
        Put("MiiName", EncodeName(name));
        File.WriteAllText(file, string.Join("\n", lines) + "\n");
    }
}
