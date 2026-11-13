using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Core.Net;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Tests;

public class NasTests
{
    private const string Lan = "ugreen-nas";
    private const string Ts = "100.101.102.103";

    private static NasLink Link(string[] hosts, params string[] up) =>
        new(() => hosts, h => up.Contains(h, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData(@"\\ugreen-nas\Games\Switch", "ugreen-nas")]
    [InlineData("//100.101.102.103/Games", "100.101.102.103")]
    [InlineData(@"D:\Games\x.nsp", null)]
    [InlineData(@"\\?\C:\Games", null)]
    [InlineData(@"\\host@8080\DavWWWRoot", null)]
    [InlineData("", null)]
    public void Unc_host_is_only_read_from_network_paths(string path, string? host) =>
        Assert.Equal(host, NasLink.UncHost(path));

    [Fact]
    public void Existing_path_moves_to_the_tailscale_address_when_the_home_name_is_down()
    {
        var nas = Link([Lan, Ts], Ts);
        Assert.Equal(@"\\100.101.102.103\Games\Switch\x.nsp", nas.Resolve(@"\\ugreen-nas\Games\Switch\x.nsp"));
        Assert.Equal(@"\\100.101.102.103/Games/x.nsp", nas.Resolve("//ugreen-nas/Games/x.nsp")); // Rest bleibt wie eingegeben
        Assert.Equal(@"\\100.101.102.103\Games", nas.Resolve(@"\\100.101.102.103\Games")); // schon die richtige Adresse
    }

    [Fact]
    public void Home_name_wins_when_both_addresses_answer_and_unreachable_nas_gives_null()
    {
        var path = @"\\100.101.102.103\Games\x.nsp";
        Assert.Equal(@"\\ugreen-nas\Games\x.nsp", Link([Lan, Ts], Lan, Ts).Resolve(path));
        Assert.Null(Link([Lan, Ts]).Resolve(path));
    }

    [Fact]
    public void Local_paths_and_unlisted_hosts_behave_as_before()
    {
        var nas = new NasLink(() => [Lan, Ts], h => h == "other-nas");
        Assert.Equal(@"D:\Games\x.nsp", nas.Resolve(@"D:\Games\x.nsp")); // lokal: nie geprüft
        Assert.Equal(@"\\other-nas\Games\x.nsp", nas.Resolve(@"\\other-nas\Games\x.nsp")); // ohne Eintrag: nur der Host selbst
        Assert.Null(nas.Resolve(@"\\dead-nas\Games\x.nsp"));
    }

    [Fact]
    public void Reachability_is_cached_and_check_reports_every_address()
    {
        var probes = 0;
        var nas = new NasLink(() => [@"\\" + Lan + @"\Games", Ts], h => { Interlocked.Increment(ref probes); return h == Ts; });
        nas.Resolve(@"\\ugreen-nas\a");
        nas.Resolve(@"\\ugreen-nas\b");
        Assert.Equal(2, probes); // eine Prüfung je Adresse, danach aus dem Zwischenspeicher

        var status = nas.Check();
        Assert.Equal(4, probes);
        Assert.Equal([new NasHostStatus(Lan, false, false), new NasHostStatus(Ts, true, true)], status);
    }

    [Theory]
    [InlineData("100.101.102.103", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("192.168.1.5", false)]
    [InlineData("nas.tail1234.ts.net", true)]
    [InlineData("ugreen-nas", false)]
    public void Tailscale_addresses_are_recognized(string host, bool tailscale) =>
        Assert.Equal(tailscale, NasLink.IsTailscale(host));

    [Fact]
    public void Games_on_an_unreachable_nas_stay_in_the_library_but_vanished_local_games_do_not()
    {
        using var hub = new TempHub();
        var db = new LibraryDatabase(hub.Paths.LibraryDb);
        // gespeicherter Pfad nutzt die Tailscale-Adresse, der Scan-Ordner den Heimnetz-Namen: dieselbe Freigabe
        db.Upsert(new GameEntry
        {
            Id = "switch:0100152000022000", Title = "MK8D", Platform = HubPlatform.Switch, EmulatorId = EmulatorIds.Switch,
            Path = @"\\100.101.102.103\Games\Switch\mk8d.nsp",
        });
        db.Upsert(new GameEntry
        {
            Id = "switch:LOKAL", Title = "Weg", Platform = HubPlatform.Switch, EmulatorId = EmulatorIds.Switch,
            Path = Path.Combine(hub.Root, "weg", "x.nsp"),
        });
        hub.Config.Update(c => c.ExtraLibraryFolders.Add(@"\\ugreen-nas\Games\Switch"));
        var lib = new LibraryService(db, hub.Paths, hub.Config, new NasLink(() => [Lan, Ts], _ => false));

        var scan = lib.Refresh([]);

        Assert.Empty(scan.Games);
        var ids = db.LoadGames().Select(g => g.Id).ToList();
        Assert.Contains("switch:0100152000022000", ids);
        Assert.DoesNotContain("switch:LOKAL", ids);
    }
}
