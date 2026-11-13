using System.Net;
using System.Net.Sockets;

namespace EmulatorPCHub.Core.Net;

/// <summary>Erreichbarkeit einer eingetragenen NAS-Adresse (für die Anzeige in den Einstellungen).</summary>
public sealed record NasHostStatus(string Host, bool Tailscale, bool Up);

/// <summary>
/// NAS (SMB-Freigabe) im Heimnetz oder über Tailscale. Prüft per TCP 445, welche der eingetragenen Adressen
/// erreichbar ist, und schreibt UNC-Pfade auf diese Adresse um: Bisherige Pfade (<c>\\ugreen-nas\Games\…</c>) bleiben
/// gültig, auch wenn unterwegs nur die Tailscale-Adresse (<c>100.x.y.z</c>) antwortet. Ein toter Host blockiert den Hub
/// so nicht minutenlang (Windows wartet bei einer nicht erreichbaren Freigabe von selbst ca. 20 s pro Zugriff).
/// Es werden nur Verbindungen zu den Hosts aufgebaut, die in deinen Pfaden bzw. in <c>nas.hosts</c> stehen.
/// </summary>
public sealed class NasLink
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(20);

    private readonly Func<IReadOnlyList<string>> _hosts;
    private readonly Func<string, bool> _probe;
    private readonly Dictionary<string, (bool Up, DateTime At)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <param name="hosts">Adressen desselben NAS in Reihenfolge der Vorliebe (zuerst LAN, dann Tailscale).</param>
    /// <param name="probe">Erreichbarkeitstest eines Hosts (Standard: TCP 445); darf nicht werfen. Für Tests austauschbar.</param>
    public NasLink(Func<IReadOnlyList<string>> hosts, Func<string, bool>? probe = null)
    {
        _hosts = hosts;
        _probe = probe ?? ProbeSmb;
    }

    private static bool IsSep(char c) => c is '\\' or '/';

    /// <summary>Host eines UNC-Pfads (<c>\\host\share</c> oder <c>//host/share</c>); null bei lokalen Pfaden, <c>\\?\</c>-Pfaden und WebDAV.</summary>
    public static string? UncHost(string? path)
    {
        if (path is not { Length: > 2 } || !IsSep(path[0]) || !IsSep(path[1]) || IsSep(path[2]))
            return null;
        var end = path.AsSpan(2).IndexOfAny('\\', '/');
        var host = end < 0 ? path[2..] : path.Substring(2, end);
        return host is "?" or "." || host.Contains('@') ? null : host;
    }

    /// <summary>„\\ugreen-nas\Games“ → „ugreen-nas“ (so wie Nutzer Adressen gern eintippen).</summary>
    public static string NormalizeHost(string host) =>
        host.Trim().TrimStart('\\', '/').Split('\\', '/')[0];

    /// <summary>Tailscale-Adresse (100.64.0.0/10 oder *.ts.net) – nur für die Anzeige.</summary>
    public static bool IsTailscale(string host) =>
        host.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
            && ip.GetAddressBytes() is [100, var b, ..] && (b & 0xC0) == 64);

    /// <summary>
    /// UNC-Pfad auf die erste erreichbare Adresse umschreiben. Steht der Host in <c>nas.hosts</c>, kommen alle dort
    /// eingetragenen Adressen infrage, sonst nur der Host des Pfads selbst. Lokale Pfade kommen unverändert zurück.
    /// </summary>
    /// <returns>Der Pfad – oder null, wenn das NAS nicht erreichbar ist.</returns>
    public string? Resolve(string path)
    {
        var host = UncHost(path);
        if (host == null)
            return path;
        var known = Hosts();
        var up = FirstReachable(known.Contains(host, StringComparer.OrdinalIgnoreCase) ? known : [host]);
        if (up == null)
            return null;
        return up.Equals(host, StringComparison.OrdinalIgnoreCase) ? path : @"\\" + up + path[(2 + host.Length)..];
    }

    /// <summary>Alle eingetragenen Adressen neu prüfen (Cache wird verworfen).</summary>
    public IReadOnlyList<NasHostStatus> Check()
    {
        lock (_lock)
            _cache.Clear();
        var hosts = Hosts();
        FirstReachable(hosts);
        lock (_lock)
            return hosts.Select(h => new NasHostStatus(h, IsTailscale(h), _cache[h].Up)).ToList();
    }

    private List<string> Hosts() =>
        _hosts().Select(NormalizeHost).Where(h => h.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private string? FirstReachable(IReadOnlyList<string> hosts)
    {
        var now = DateTime.UtcNow;
        List<string> stale;
        lock (_lock)
            stale = hosts.Where(h => !_cache.TryGetValue(h, out var c) || now - c.At > CacheTime).ToList();
        // alle Adressen gleichzeitig prüfen: unerreichbar kostet so einmal das Zeitlimit, nicht je Adresse
        var probes = stale.Select(h => Task.Run(() => (h, up: _probe(h)))).ToArray();
        Task.WaitAll(probes);
        lock (_lock)
        {
            foreach (var p in probes)
                _cache[p.Result.h] = (p.Result.up, now);
            return hosts.FirstOrDefault(h => _cache[h].Up);
        }
    }

    private static bool ProbeSmb(string host)
    {
        try
        {
            using var tcp = new TcpClient();
            return tcp.ConnectAsync(host, 445).Wait(1500) && tcp.Connected;
        }
        catch (Exception)
        {
            return false; // Name nicht auflösbar, abgelehnt, kein Netz
        }
    }
}
