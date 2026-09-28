using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Input;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Tests;

public class EdenTests
{
    private const string DualSenseMapping =
        "050000004c050000e60c000000810000,PS5 Controller,a:b0,b:b1,back:b4,dpdown:b12,dpleft:b13,dpright:b14,dpup:b11," +
        "guide:b5,leftshoulder:b9,leftstick:b7,lefttrigger:a4,leftx:a0,lefty:a1,misc1:b15,rightshoulder:b10,rightstick:b8," +
        "righttrigger:a5,rightx:a2,righty:a3,start:b6,x:b2,y:b3,touchpad:b16,platform:Windows,";

    private static readonly ControllerDescriptor DualSense = new()
    {
        Key = "ds", Name = "DualSense Wireless Controller", Kind = ControllerKind.PlayStation5,
        SdlGuid = "050012344c050000e60c000000810000", SdlMapping = DualSenseMapping,
    };

    private static (TempHub hub, EdenAdapter eden) Setup()
    {
        var hub = new TempHub();
        hub.File("integrations/switch/eden.exe");
        Directory.CreateDirectory(Path.Combine(hub.Root, "integrations/switch/user"));
        return (hub, new EdenAdapter(hub.Paths, hub.Config, hub.Backups));
    }

    [Fact]
    public void Eden_is_detected_portable_with_keys_firmware_and_mod_paths()
    {
        var (hub, eden) = Setup();
        using (hub)
        {
            var data = Path.Combine(hub.Root, "integrations", "switch", "user");
            Assert.Equal(data, eden.DataDirectory());
            var inst = eden.DetectInstallation();
            Assert.True(inst.IsInstalled && inst.Portable);
            Assert.False(eden.GetSystemStatus().KeysPresent);

            hub.File("integrations/switch/user/keys/prod.keys");
            hub.File("integrations/switch/user/nand/system/Contents/registered/a.nca");
            var sys = eden.GetSystemStatus();
            Assert.True(sys.KeysPresent && sys.FirmwarePresent);
            Assert.Equal(Path.Combine(data, "load", "0100152000022000"), eden.ModsDirectory("0100152000022000"));
        }
    }

    [Fact]
    public void Eden_launch_uses_fullscreen_and_game_flags()
    {
        var (hub, eden) = Setup();
        using (hub)
        {
            hub.File("integrations/switch/user/keys/prod.keys");
            var game = hub.File("games/mk8.nsp");
            var spec = eden.PrepareLaunch(new LaunchRequest { Game = new GameEntry { Id = "g", Title = "MK8D", Path = game }, Fullscreen = true });
            Assert.Equal(["-f", "-g", game], spec.Arguments);
        }
    }

    [Fact]
    public void Eden_game_folder_and_external_content_are_written_to_qt_config()
    {
        var (hub, eden) = Setup();
        using (hub)
        {
            eden.AddGameFolder(@"D:\Emulator PC Hub Games\Switch");
            eden.AddGameFolder(@"D:\Emulator PC Hub Games\Switch"); // doppelt → nur einmal
            eden.RegisterAddOns(KnownGames.MarioKart8DeluxeTitleId,
                [new SwitchAddOnFile(@"D:\Switch\Updates\u.nsp", "0100152000022800", [], 1507328)],
                [new SwitchAddOnFile(@"D:\Switch\DLC\dlc1.nsp", "0100152000023001", ["aaaa.nca"], null)]);

            var ini = EdenIni.Load(eden.ConfigFile);
            Assert.Equal(["SDMC", "UserNAND", "SysNAND", "D:/Emulator PC Hub Games/Switch"], ini.GetArray("UI", @"Paths\gamedirs", "path"));
            Assert.Equal("true", ini.Get("UI", @"Paths\gamedirs\4\deep_scan"));
            Assert.Equal(["D:/Switch/Updates", "D:/Switch/DLC"], ini.GetArray("UI", @"Paths\external_content_dirs", "path"));
            Assert.Equal(@"D:\Switch\Updates\u.nsp", eden.SelectedUpdatePath(KnownGames.MarioKart8DeluxeTitleId));
        }
    }

    [Fact]
    public void Eden_guid_clears_name_crc()
    {
        Assert.Equal("050000004c050000e60c000000810000", EdenInputWriter.EdenGuid("050012344c050000e60c000000810000"));
    }

    [Fact]
    public void Eden_writer_applies_button_remap()
    {
        var ini = new EdenIni();
        var remap = new List<ButtonRemap>();
        ButtonMap.Assign(remap, EmulatedPad.SwitchPro, false, "A", PadButton.South); // A ↔ B tauschen
        ButtonMap.Assign(remap, EmulatedPad.SwitchPro, false, "ZL", null);
        var setup = new InputSetup { Remap = remap };
        setup.Players.Add(new ResolvedPlayer(1, DualSense, PlayerMode.Native, EmulatedPad.SwitchPro, ""));
        EdenInputWriter.Write(ini, setup);

        const string dev = "port:0,guid:050000004c050000e60c000000810000";
        Assert.Equal($"engine:sdl,{dev},button:0", ini.Get("Controls", "player_0_button_a"));
        Assert.Equal($"engine:sdl,{dev},button:1", ini.Get("Controls", "player_0_button_b"));
        Assert.Equal("[empty]", ini.Get("Controls", "player_0_button_zl"));
    }

    [Fact]
    public void Eden_writer_maps_dualsense_by_position_and_keyboard_to_defaults()
    {
        var ini = new EdenIni();
        var setup = new InputSetup { Rumble = true };
        setup.Players.Add(new ResolvedPlayer(1, DualSense with { GuidIndex = 1 }, PlayerMode.Native, EmulatedPad.SwitchPro, ""));
        setup.Players.Add(new ResolvedPlayer(2, ControllerDescriptor.Keyboard, PlayerMode.Native, EmulatedPad.SwitchPro, ""));
        EdenInputWriter.Write(ini, setup);

        const string dev = "port:1,guid:050000004c050000e60c000000810000";
        Assert.Equal("true", ini.Get("Controls", "player_0_connected"));
        Assert.Equal("0", ini.Get("Controls", "player_0_type"));
        Assert.Equal("false", ini.Get("Controls", @"player_0_button_a\default"));
        // Switch-A = rechte Taste (Kreis = b1), Switch-B = untere Taste (Kreuz = b0)
        Assert.Equal($"engine:sdl,{dev},button:1", ini.Get("Controls", "player_0_button_a"));
        Assert.Equal($"engine:sdl,{dev},button:0", ini.Get("Controls", "player_0_button_b"));
        Assert.Equal($"engine:sdl,{dev},axis:4,threshold:0.5,invert:+", ini.Get("Controls", "player_0_button_zl"));
        Assert.Equal($"engine:sdl,{dev},button:11", ini.Get("Controls", "player_0_button_dup"));
        Assert.StartsWith($"engine:sdl,{dev},axis_x:0,axis_y:1,", ini.Get("Controls", "player_0_lstick"));
        Assert.Equal($"engine:sdl,motion:0,{dev}", ini.Get("Controls", "player_0_motionleft"));

        Assert.Equal("true", ini.Get("Controls", "player_1_connected"));
        Assert.Equal("true", ini.Get("Controls", @"player_1_button_a\default"));
        Assert.Null(ini.Get("Controls", "player_1_button_a"));
        Assert.Equal("false", ini.Get("Controls", "player_2_connected"));
    }

    [Fact]
    public void Eden_ini_quotes_param_strings_like_eden()
    {
        var ini = new EdenIni();
        ini.Set("Controls", "player_0_button_a", "engine:sdl,port:0,guid:x,button:1");
        var file = Path.Combine(Path.GetTempPath(), "eden-ini-" + Guid.NewGuid().ToString("N")[..8] + ".ini");
        try
        {
            ini.Save(file);
            var text = File.ReadAllText(file);
            Assert.Contains("[Controls]\nplayer_0_button_a\\default=false\nplayer_0_button_a=\"engine:sdl,port:0,guid:x,button:1\"", text);
            Assert.Equal("engine:sdl,port:0,guid:x,button:1", EdenIni.Load(file).Get("Controls", "player_0_button_a"));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
