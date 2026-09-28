using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EmulatorPCHub.Core.Net;

namespace EmulatorPCHub.Updates;

/// <summary>Eine veröffentlichte Version einer Komponente (nur Infos – der Hub lädt nichts herunter).</summary>
public sealed record ReleaseInfo(
    string Version,
    string? Changelog,
    string? ReleasePage,
    DateTimeOffset? Published);

/// <summary>Quelle für Versionsinfos (offizielle Feeds). Es werden nur Versionsnummer und Changelog gelesen.</summary>
public interface IReleaseSource
{
    string Description { get; }
    Task<ReleaseInfo?> LatestAsync(CancellationToken ct);
}

/// <summary>
/// GitHub Releases (Cemu, Wheel Wizard, WiiCompiled) – mit <c>host</c> auch Forgejo/Gitea-Instanzen
/// wie git.eden-emu.dev (gleiches JSON-Format unter /api/v1).
/// </summary>
public sealed class GitHubReleaseSource(string owner, string repo, string? host = null) : IReleaseSource
{
    public string Description => $"{host ?? "github.com"}/{owner}/{repo}";

    private string ApiUrl => host == null
        ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
        : $"https://{host}/api/v1/repos/{owner}/{repo}/releases/latest";

    public async Task<ReleaseInfo?> LatestAsync(CancellationToken ct)
    {
        var json = await OnlineInfo.GetStringAsync(ApiUrl, ct);
        var node = JsonNode.Parse(json);
        if (node == null)
            return null;
        var tag = node["tag_name"]?.GetValue<string>() ?? "?";
        return new ReleaseInfo(
            tag.TrimStart('v', 'V'),
            node["body"]?.GetValue<string>(),
            node["html_url"]?.GetValue<string>(),
            DateTimeOffset.TryParse(node["published_at"]?.GetValue<string>(), out var d) ? d : null);
    }
}

/// <summary>Offizielle Dolphin-Update-API (dolphin-emu.org).</summary>
public sealed class DolphinReleaseSource : IReleaseSource
{
    public string Description => "dolphin-emu.org";

    public async Task<ReleaseInfo?> LatestAsync(CancellationToken ct)
    {
        var json = await OnlineInfo.GetStringAsync("https://dolphin-emu.org/update/latest/beta/", ct);
        var node = JsonNode.Parse(json);
        if (node == null)
            return null;
        var changelog = node["changelog_html"]?.GetValue<string>();
        if (changelog != null)
            changelog = Regex.Replace(changelog, "<[^>]+>", "").Trim();
        return new ReleaseInfo(node["shortrev"]?.GetValue<string>() ?? "?", changelog, "https://dolphin-emu.org/download/",
            DateTimeOffset.TryParse(node["date"]?.GetValue<string>(), out var d) ? d : null);
    }
}

/// <summary>Retro Rewind (offizieller Server update.rwfc.net).</summary>
public sealed class RetroRewindReleaseSource : IReleaseSource
{
    public string Description => "update.rwfc.net (Retro Rewind)";

    public async Task<ReleaseInfo?> LatestAsync(CancellationToken ct)
    {
        var text = await OnlineInfo.GetStringAsync("https://update.rwfc.net/RetroRewind/RetroRewindVersion.txt", ct);
        var last = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (last == null)
            return null;
        return new ReleaseInfo(last.Split(' ')[0], null, "https://rwfc.net", null);
    }
}
