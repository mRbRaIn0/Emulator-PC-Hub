using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Switch2;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Tests;

public class Switch2Tests
{
    private const string Mkw = "Mario Kart World [0400C3F00006E000][v917504].nup";

    [Fact]
    public void Scanner_finds_nup_as_switch2_game_with_title_id_from_file_name()
    {
        using var hub = new TempHub();
        hub.File("lib/Switch2/MKW/" + Mkw, new byte[16]);
        hub.File("lib/Switch/other.nup", new byte[16]); // Switch-1-Ordner ignoriert .nup
        var result = new GameScanner().Scan(
        [
            new ScanRoot(Path.Combine(hub.Root, "lib", "Switch2"), HubPlatform.Switch2),
            new ScanRoot(Path.Combine(hub.Root, "lib", "Switch"), HubPlatform.Switch),
        ], TestContext.Current.CancellationToken);

        var game = Assert.Single(result.Games);
        Assert.Equal(HubPlatform.Switch2, game.Platform);
        Assert.Equal("Mario Kart World", game.Title);
        Assert.Equal("0400C3F00006E000", game.GameCode);
        Assert.Equal("switch2:0400C3F00006E000", game.Id);
        Assert.Equal(EmulatorIds.Eglantine, game.EmulatorId);
    }

    [Fact]
    public void Eglantine_launches_with_fullscreen_and_game_flag_and_disables_update_check()
    {
        using var hub = new TempHub();
        hub.File("integrations/eglantine/eglantine.exe");
        var ini = hub.File("integrations/eglantine/user/config/qt-config.ini",
            System.Text.Encoding.UTF8.GetBytes("[UI]\ncheck_for_updates\\default=true\ncheck_for_updates=true\n"));
        var game = hub.File("games/" + Mkw, new byte[16]);

        var adapter = new EglantineAdapter(hub.Paths, hub.Config);
        Assert.True(adapter.DetectInstallation().IsInstalled);
        var spec = adapter.PrepareLaunch(new LaunchRequest { Game = new GameEntry { Id = "switch2:x", Title = "MKW", Path = game }, Fullscreen = true });

        Assert.Equal(["-f", "-g", game], spec.Arguments);
        var text = File.ReadAllText(ini);
        Assert.Contains("check_for_updates=false", text);
    }

    [Fact]
    public void Eglantine_reports_missing_installation_as_launch_error()
    {
        using var hub = new TempHub();
        var game = hub.File("games/" + Mkw, new byte[16]);
        var adapter = new EglantineAdapter(hub.Paths, hub.Config);
        Assert.False(adapter.DetectInstallation().IsInstalled);
        Assert.Throws<LaunchException>(() =>
            adapter.PrepareLaunch(new LaunchRequest { Game = new GameEntry { Id = "x", Title = "x", Path = game } }));
    }
}
