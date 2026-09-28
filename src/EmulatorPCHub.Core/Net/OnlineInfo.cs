using EmulatorPCHub.Core;
using System.Net.Http.Headers;

namespace EmulatorPCHub.Core.Net;

/// <summary>
/// Liest ausschließlich kleine Textinfos (Versionsnummern, Changelogs) aus offiziellen Release-Feeds.
/// Der Hub lädt keine Dateien herunter – Emulatoren, Tools und Mods stellt der Nutzer selbst bereit.
/// </summary>
public static class OnlineInfo
{
    public static HttpClient Http { get; } = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 10 })
        {
            Timeout = TimeSpan.FromMinutes(1),
        };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EmulatorPCHub", HubInfo.DisplayVersion));
        return c;
    }

    public static async Task<string> GetStringAsync(string url, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(20));
        return await Http.GetStringAsync(url, cts.Token);
    }
}
