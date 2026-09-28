using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Input;

namespace EmulatorPCHub.Tests;

public class ControllerAdviceTests
{
    private static GameEntry Game(HubPlatform p, string title, string? code = null, SpecialPage special = SpecialPage.None) =>
        new() { Id = title, Title = title, Platform = p, GameCode = code, Special = special };

    private static InputFit Fit(ControllerAdviceResult r, ControllerKind k) => r.Items.Single(i => i.Kind == k).Fit;

    [Fact]
    public void WiiSportsResort_NeedsWiiRemote_MotionPlus()
    {
        var r = ControllerAdvisor.For(Game(HubPlatform.Wii, "Wii Sports Resort", "RZTP01"), EmulatorIds.Dolphin);
        Assert.Equal(InputFit.Recommended, Fit(r, ControllerKind.WiiRemote));
        Assert.Equal(InputFit.No, Fit(r, ControllerKind.PlayStation5));
        Assert.Equal(InputFit.No, Fit(r, ControllerKind.Keyboard));
        Assert.Equal("Wii Remote", r.Best!.Label);
    }

    [Fact]
    public void GameCube_WiiRemoteNotCompatible_PadRecommended()
    {
        var r = ControllerAdvisor.For(Game(HubPlatform.GameCube, "Mario Kart: Double Dash!!", "GM4P01"), EmulatorIds.Dolphin);
        Assert.Equal(InputFit.No, Fit(r, ControllerKind.WiiRemote));
        Assert.Equal(InputFit.Recommended, Fit(r, ControllerKind.PlayStation5));
        Assert.Equal(InputFit.Works, Fit(r, ControllerKind.Keyboard));
    }

    [Fact]
    public void WiiU_WiiRemoteOnlyGames_RejectProController()
    {
        foreach (var title in new[] { "Mario Party 10", "Wii Party U", "JUST DANCE 2016", "Wii Sports Club" })
        {
            var r = ControllerAdvisor.For(Game(HubPlatform.WiiU, title), EmulatorIds.Cemu);
            Assert.Equal(InputFit.No, Fit(r, ControllerKind.PlayStation5));
            Assert.Equal(InputFit.Recommended, Fit(r, ControllerKind.WiiRemote));
        }
        var botw = ControllerAdvisor.For(Game(HubPlatform.WiiU, "The Legend of Zelda Breath of the Wild"), EmulatorIds.Cemu);
        Assert.Equal(InputFit.No, Fit(botw, ControllerKind.WiiRemote));
        Assert.Equal(InputFit.Recommended, Fit(botw, ControllerKind.PlayStation5));
    }

    [Fact]
    public void MarioKartWii_WiiRemoteDependsOnEngine()
    {
        var mk = Game(HubPlatform.Wii, "Mario Kart Wii", null, SpecialPage.MarioKartWii);
        Assert.Equal(InputFit.No, Fit(ControllerAdvisor.For(mk, EmulatorIds.WiiCompiled), ControllerKind.WiiRemote));
        Assert.Equal(InputFit.Works, Fit(ControllerAdvisor.For(mk, EmulatorIds.Dolphin), ControllerKind.WiiRemote));
        Assert.Equal(InputFit.Recommended, Fit(ControllerAdvisor.For(mk, EmulatorIds.Dolphin), ControllerKind.PlayStation5));
    }

    [Fact]
    public void WiiTitleKeywords_DoNotLeakToWiiU()
    {
        Assert.Equal(ControllerAdvisor.PlayStyle.WiiRemoteOnly, ControllerAdvisor.StyleFor(Game(HubPlatform.WiiU, "Wii Sports Club")));
        Assert.Equal(ControllerAdvisor.PlayStyle.Motion, ControllerAdvisor.StyleFor(Game(HubPlatform.Wii, "Wii Sports")));
        Assert.Equal(ControllerAdvisor.PlayStyle.Buttons, ControllerAdvisor.StyleFor(Game(HubPlatform.WiiU, "Skylanders Giants")));
        Assert.Equal(ControllerAdvisor.PlayStyle.Pointer, ControllerAdvisor.StyleFor(Game(HubPlatform.Wii, "Unbekanntes Spiel", "XYZP01")));
        Assert.Equal(ControllerAdvisor.PlayStyle.Handheld, ControllerAdvisor.StyleFor(Game(HubPlatform.ThreeDS, "Pokemon Y")));
    }

    [Fact]
    public void Handheld_KeyboardByDefault_ControllerForActionGames()
    {
        var pokemon = ControllerAdvisor.For(Game(HubPlatform.ThreeDS, "Pokemon Y"), EmulatorIds.Azahar);
        Assert.Equal(InputFit.Recommended, Fit(pokemon, ControllerKind.Keyboard));
        Assert.Equal(InputFit.Works, Fit(pokemon, ControllerKind.PlayStation5));
        var mk = ControllerAdvisor.For(Game(HubPlatform.DS, "Mario Kart DS"), EmulatorIds.MelonDS);
        Assert.Equal(InputFit.Recommended, Fit(mk, ControllerKind.PlayStation5));
        Assert.Equal(InputFit.Works, Fit(mk, ControllerKind.Keyboard));
        var minis = ControllerAdvisor.For(Game(HubPlatform.DS, "Mario vs Donkey Kong - Mini-Land Mayhem!"), EmulatorIds.MelonDS);
        Assert.Equal(InputFit.Limited, Fit(minis, ControllerKind.PlayStation5));
        Assert.Equal(InputFit.No, Fit(minis, ControllerKind.WiiRemote));
    }
}
