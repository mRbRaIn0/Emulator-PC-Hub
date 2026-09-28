using System.Buffers.Binary;
using System.Text;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;

namespace EmulatorPCHub.Tests;

/// <summary>Temporärer Hub-Stammordner für Tests.</summary>
public sealed class TempHub : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "npchub-test-" + Guid.NewGuid().ToString("N")[..8]);
    public AppPaths Paths { get; }
    public BackupService Backups { get; }
    public ConfigService Config { get; }

    public TempHub()
    {
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
        Backups = new BackupService(Paths.Backups);
        Config = new ConfigService(Paths, Backups);
        Config.Load();
    }

    public string File(string relative, byte[]? content = null)
    {
        var p = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        System.IO.File.WriteAllBytes(p, content ?? []);
        return p;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { }
    }
}

public static class Fake
{
    /// <summary>Minimaler Disc-Header (ISO) mit ID, Titel und Wii-/GC-Magic.</summary>
    public static byte[] DiscHeader(string id, string title, bool wii)
    {
        var b = new byte[0x440];
        Encoding.ASCII.GetBytes(id).CopyTo(b, 0);
        if (wii)
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(0x18), 0x5D1C9EA3);
        else
            BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(0x1C), 0xC2339F3D);
        Encoding.ASCII.GetBytes(title).CopyTo(b, 0x20);
        return b;
    }

    /// <summary>RVZ-Datei: Header 1 (0x48) + Header 2 mit Disc-Header-Kopie bei 0x58.</summary>
    public static byte[] Rvz(string id, string title, bool wii)
    {
        var b = new byte[0x200];
        Encoding.ASCII.GetBytes("RVZ\u0001").CopyTo(b, 0);
        DiscHeader(id, title, wii).AsSpan(0, 0x80).CopyTo(b.AsSpan(0x58));
        return b;
    }

    /// <summary>Minimales NSP (PFS0) mit Dateinamen.</summary>
    public static byte[] Nsp(params string[] names)
    {
        var strings = new MemoryStream();
        var offsets = new List<int>();
        foreach (var n in names)
        {
            offsets.Add((int)strings.Length);
            strings.Write(Encoding.ASCII.GetBytes(n));
            strings.WriteByte(0);
        }
        while (strings.Length % 16 != 0)
            strings.WriteByte(0);
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("PFS0"));
        var tmp = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(tmp, names.Length); ms.Write(tmp);
        BinaryPrimitives.WriteInt32LittleEndian(tmp, (int)strings.Length); ms.Write(tmp);
        ms.Write(new byte[4]);
        foreach (var o in offsets)
        {
            ms.Write(new byte[16]); // offset + size
            BinaryPrimitives.WriteInt32LittleEndian(tmp, o); ms.Write(tmp);
            ms.Write(new byte[4]);
        }
        strings.Position = 0;
        strings.CopyTo(ms);
        return ms.ToArray();
    }
}
