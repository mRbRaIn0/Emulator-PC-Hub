using System.Text.Json.Nodes;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Input;

namespace EmulatorPCHub.Tests;

public class InputTests
{
    private static readonly ControllerDescriptor Xbox = new()
    {
        Key = "x", Name = "Xbox Series X Controller", Kind = ControllerKind.Xbox, XInputIndex = 0,
        SdlGuid = "030000005e040000130b000000007200",
    };

    private static readonly ControllerDescriptor DualSense = new()
    {
        Key = "ds", Name = "DualSense Wireless Controller", Kind = ControllerKind.PlayStation5,
        SdlGuid = "050000004c050000e60c000000810000",
    };

    private static readonly ControllerDescriptor SwitchPro = new()
    {
        Key = "sp", Name = "Nintendo Switch Pro Controller", Kind = ControllerKind.SwitchPro, NintendoLayout = true,
        SdlGuid = "050000007e0500000920000000006800",
    };

    private static readonly ControllerDescriptor Wiimote = new()
    {
        Key = "wm", Name = "Nintendo Wii Remote", Kind = ControllerKind.WiiRemote, NintendoLayout = true,
        SdlGuid = "050000007e0500000603000000006800",
    };

    [Theory]
    [InlineData(EmulatorIds.Dolphin, ControllerKind.Xbox, PlayerMode.Native)]
    [InlineData(EmulatorIds.Switch, ControllerKind.Xbox, PlayerMode.Native)]
    [InlineData(EmulatorIds.Switch, ControllerKind.Keyboard, PlayerMode.Native)]
    [InlineData(EmulatorIds.Switch, ControllerKind.PlayStation5, PlayerMode.Native)]
    [InlineData(EmulatorIds.Switch, ControllerKind.WiiRemote, PlayerMode.Compatibility)]
    [InlineData(EmulatorIds.Dolphin, ControllerKind.WiiRemote, PlayerMode.Native)]
    [InlineData(EmulatorIds.Cemu, ControllerKind.WiiUPro, PlayerMode.Native)]
    [InlineData(EmulatorIds.Cemu, ControllerKind.Generic, PlayerMode.Compatibility)]
    public void Xbox_native_others_only_compat_when_unsupported(string backend, ControllerKind kind, PlayerMode expected)
    {
        Assert.Equal(expected, CompatibilityPolicy.Decide(backend, kind, PlayerMode.Auto).Mode);
    }

    [Fact]
    public void Manual_mode_overrides_policy()
    {
        Assert.Equal(PlayerMode.Compatibility, CompatibilityPolicy.Decide(EmulatorIds.Dolphin, ControllerKind.Xbox, PlayerMode.Compatibility).Mode);
    }

    [Fact]
    public void Mario_kart_wii_uses_gamecube_layout_for_pads_and_real_wiimote_for_wiimotes()
    {
        var mk = new GameEntry { Id = KnownGames.MarioKartWiiId, Title = "Mario Kart Wii", Platform = HubPlatform.Wii, Special = SpecialPage.MarioKartWii };
        Assert.Equal(EmulatedPad.GameCube, CompatibilityPolicy.DefaultPad(EmulatorIds.Dolphin, mk, DualSense));
        Assert.Equal(EmulatedPad.Wiimote, CompatibilityPolicy.DefaultPad(EmulatorIds.Dolphin, mk, Wiimote));
    }

    [Fact]
    public void Switch_motion_games_emulate_joycons_others_pro_controller()
    {
        var sports = new GameEntry { Id = "sports", Title = "Nintendo Switch Sports", Platform = HubPlatform.Switch, GameCode = "0100D2F00D5C0000" };
        var tomodachi = new GameEntry { Id = "tomo", Title = "Tomodachi Life", Platform = HubPlatform.Switch, GameCode = "010051F0207B2000" };
        Assert.Equal(EmulatedPad.SwitchJoyConPair, CompatibilityPolicy.DefaultPad(EmulatorIds.Switch, sports, DualSense));
        Assert.Equal(EmulatedPad.SwitchPro, CompatibilityPolicy.DefaultPad(EmulatorIds.Switch, tomodachi, DualSense));
    }

    [Fact]
    public void Dolphin_writer_maps_xinput_sdl_keyboard_and_real_wiimote()
    {
        using var hub = new TempHub();
        var user = Path.Combine(hub.Root, "DolphinUser");
        var setup = new InputSetup();
        setup.Players.Add(new ResolvedPlayer(1, Xbox, PlayerMode.Native, EmulatedPad.GameCube, ""));
        setup.Players.Add(new ResolvedPlayer(2, SwitchPro, PlayerMode.Native, EmulatedPad.GameCube, ""));
        setup.Players.Add(new ResolvedPlayer(3, ControllerDescriptor.Keyboard, PlayerMode.Native, EmulatedPad.GameCube, ""));
        setup.Players.Add(new ResolvedPlayer(4, Wiimote, PlayerMode.Native, EmulatedPad.Wiimote, ""));
        new DolphinInputWriter(user, hub.Backups).Apply(setup, new GameEntry { Id = "g", Title = "MKWii" });

        var gc = File.ReadAllText(Path.Combine(user, "Config", "GCPadNew.ini"));
        Assert.Contains("Device = XInput/0/Gamepad", gc);
        Assert.Contains("Device = SDL/0/Nintendo Switch Pro Controller", gc);
        Assert.Contains("Buttons/A = `Button E`", gc); // Nintendo-Layout: A rechts
        Assert.Contains("Device = DInput/0/Keyboard Mouse", gc);
        Assert.Contains("Main Stick/Up = `Left Y+`", gc);
        var wm = new IniFile(Path.Combine(user, "Config", "WiimoteNew.ini"));
        Assert.Equal("2", wm.Get("Wiimote4", "Source"));
        Assert.Equal("0", wm.Get("Wiimote1", "Source"));
        var dolphin = new IniFile(Path.Combine(user, "Config", "Dolphin.ini"));
        Assert.Equal("6", dolphin.Get("Core", "SIDevice0"));
        Assert.Equal("0", dolphin.Get("Core", "SIDevice3"));
    }

    [Fact]
    public void Cemu_profile_uses_xinput_for_xbox_and_sdl_uuid_for_others()
    {
        var xbox = CemuInputWriter.BuildProfile(new ResolvedPlayer(1, Xbox, PlayerMode.Native, EmulatedPad.WiiUPro, ""), true);
        Assert.Equal("Wii U Pro Controller", xbox.Root!.Element("type")!.Value);
        Assert.Equal("XInput", xbox.Root.Element("controller")!.Element("api")!.Value);
        Assert.Equal("0", xbox.Root.Element("controller")!.Element("uuid")!.Value);

        var ds = CemuInputWriter.BuildProfile(new ResolvedPlayer(2, DualSense with { GuidIndex = 1 }, PlayerMode.Native, EmulatedPad.WiiUPro, ""), true);
        Assert.Equal("SDLController", ds.Root!.Element("controller")!.Element("api")!.Value);
        Assert.Equal("1_050000004c050000e60c000000810000", ds.Root.Element("controller")!.Element("uuid")!.Value);
        Assert.Equal(25, ds.Root.Element("controller")!.Element("mappings")!.Elements("entry").Count());
    }

    [Fact]
    public void Profile_store_persists_assignments_and_per_game_profiles()
    {
        using var hub = new TempHub();
        var store = new EmulatorPCHub.UI.Services.ControllerProfileStore(hub.Paths);
        store.SetAssignment("dev1", 2);
        var p = store.GetOrCreate("wii:RMCP01");
        p.For(1).Mode = PlayerMode.Compatibility;
        p.KeyboardPlayer = 3;
        store.Save();

        var again = new EmulatorPCHub.UI.Services.ControllerProfileStore(hub.Paths);
        Assert.Equal(2, again.GetAssignment("dev1"));
        Assert.True(again.HasOwnProfile("wii:RMCP01"));
        Assert.Equal(PlayerMode.Compatibility, again.For("wii:RMCP01").For(1).Mode);
        Assert.Equal(3, again.For("wii:RMCP01").KeyboardPlayer);
        Assert.False(again.HasOwnProfile("other"));
    }
}
