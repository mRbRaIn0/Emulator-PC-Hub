using System.IO.Compression;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Net;
using EmulatorPCHub.Emulation.Dolphin;

namespace EmulatorPCHub.Mods;

public sealed record RetroRewindStatus(bool Installed, string? Version, string? LatestVersion, string? Folder)
{
    public bool UpdateAvailable => Installed && LatestVersion != null && Version != null &&
                                   CompareVersions(LatestVersion, Version) > 0;

    public static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.', '-').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var pb = b.Split('.', '-').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;
            if (x != y)
                return x.CompareTo(y);
        }
        return 0;
    }
}

/// <summary>
/// Retro Rewind (Plan Abschnitt 7): Version erkennen, Updates anzeigen, Installation prüfen und installieren.
/// Nutzt dieselbe Ordnerstruktur wie Wheel Wizard (<c>Load/Riivolution/WheelWizard</c> im Dolphin-Userordner),
/// damit Hub, Wheel Wizard und WiiCompiled dieselbe Installation verwenden.
/// </summary>
public sealed class RetroRewindService
{
    public const string ServerBase = "https://update.rwfc.net/RetroRewind/";
    private readonly DolphinAdapter _dolphin;
    private readonly AppPaths _paths;

    public RetroRewindService(DolphinAdapter dolphin, AppPaths paths)
    {
        _dolphin = dolphin;
        _paths = paths;
    }

    public RetroRewindLayout? Layout => RetroRewindLayout.Find(_dolphin.UserDirectory());

    public RetroRewindStatus LocalStatus()
    {
        var layout = Layout;
        return new RetroRewindStatus(layout != null, layout?.Version, null, layout?.DataFolder);
    }

    public async Task<RetroRewindStatus> StatusAsync(CancellationToken ct = default)
    {
        var local = LocalStatus();
        var latest = await LatestVersionAsync(ct);
        return local with { LatestVersion = latest };
    }

    /// <summary>Liest die neueste Version vom offiziellen Retro-Rewind-Server.</summary>
    public static async Task<string?> LatestVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var text = await OnlineInfo.GetStringAsync(ServerBase + "RetroRewindVersion.txt", ct);
            var last = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            return last?.Split(' ')[0];
        }
        catch (Exception ex)
        {
            HubLog.Warn("Retro-Rewind-Server nicht erreichbar", ex);
            return null;
        }
    }

    /// <summary>
    /// Installiert ein vom Nutzer selbst heruntergeladenes Retro-Rewind-Komplettpaket (ZIP) und entpackt
    /// <c>RetroRewind6/</c> und <c>riivolution/</c> in den Dolphin-Userordner. Der Hub lädt nichts herunter.
    /// </summary>
    public async Task InstallFromFileAsync(string zip, IProgress<string>? progress, CancellationToken ct)
    {
        if (!File.Exists(zip))
            throw new FileNotFoundException("Paket nicht gefunden.", zip);
        using (var check = ZipFile.OpenRead(zip))
        {
            if (!check.Entries.Any(e => e.FullName.Replace('\\', '/').StartsWith("RetroRewind6/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"„{Path.GetFileName(zip)}“ enthält keinen Ordner „RetroRewind6“ – bitte das vollständige Retro-Rewind-Paket wählen.");
        }

        progress?.Report("Entpacke Retro Rewind …");
        var root = RetroRewindLayout.DefaultRoot(_dolphin.UserDirectory());
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zip);
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var name = entry.FullName.Replace('\\', '/');
                if (!name.StartsWith("RetroRewind6/", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("riivolution/", StringComparison.OrdinalIgnoreCase))
                    continue;
                var dest = Path.GetFullPath(Path.Combine(root, name));
                if (!dest.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                    continue; // Schutz vor Pfad-Manipulation im Archiv
                if (name.EndsWith('/'))
                {
                    Directory.CreateDirectory(dest);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }, ct);
        HubLog.Info($"Retro Rewind installiert nach {root}");
    }
}
