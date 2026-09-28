using System.Text;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Handheld;
using EmulatorPCHub.Emulation.Input;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Tests;

public class HandheldTests
{
    private static byte[] NdsHeader(string title, string code)
    {
        var b = new byte[0x200];
        Encoding.ASCII.GetBytes(title).CopyTo(b, 0);
        Encoding.ASCII.GetBytes(code).CopyTo(b, 0x0C);
        return b;
    }

    [Fact]
    public void Scanner_detects_ds_and_3ds_games_with_ds_game_code()
    {
        using var hub = new TempHub();
        hub.File("lib/DS/MARIO KART DS.nds", NdsHeader("MARIOKARTDS", "AMCP"));
        hub.File("lib/3DS/Some Game.3ds", new byte[0x200]);
        var result = new GameScanner().Scan([new ScanRoot(Path.Combine(hub.Root, "lib"), null)], TestContext.Current.CancellationToken);

        var ds = Assert.Single(result.Games, g => g.Platform == HubPlatform.DS);
        Assert.Equal("AMCP", ds.GameCode);
        Assert.Equal("ds:AMCP", ds.Id);
        Assert.Equal(EmulatorIds.MelonDS, ds.EmulatorId);
        var n3ds = Assert.Single(result.Games, g => g.Platform == HubPlatform.ThreeDS);
        Assert.Equal(EmulatorIds.Azahar, n3ds.EmulatorId);
    }

    [Fact]
    public void Ds_root_ignores_3ds_files()
    {
        using var hub = new TempHub();
        hub.File("lib/a.3ds", new byte[0x200]);
        var result = new GameScanner().Scan([new ScanRoot(Path.Combine(hub.Root, "lib"), HubPlatform.DS)], TestContext.Current.CancellationToken);
        Assert.Empty(result.Games);
    }

    [Fact]
    public void MelonDS_and_Azahar_launch_with_fullscreen_flag_before_game()
    {
        using var hub = new TempHub();
        hub.File("integrations/melonds/melonDS-1.1/melonDS.exe");
        hub.File("integrations/azahar/azahar-windows-msvc/azahar.exe");
        var game = hub.File("games/mkds.nds");

        var melon = new MelonDsAdapter(hub.Paths, hub.Config);
        Assert.True(melon.DetectInstallation().IsInstalled);
        var spec = melon.PrepareLaunch(new LaunchRequest { Game = new GameEntry { Id = "g", Title = "MKDS", Path = game }, Fullscreen = true });
        Assert.Equal(["-f", game], spec.Arguments);

        var azahar = new AzaharAdapter(hub.Paths, hub.Config);
        var windowed = azahar.PrepareLaunch(new LaunchRequest { Game = new GameEntry { Id = "h", Title = "3DS", Path = game }, Fullscreen = false });
        Assert.Equal([game], windowed.Arguments);
        Assert.EndsWith(Path.Combine("azahar-windows-msvc", "user"), azahar.DetectInstallation().UserDataDir);
    }

    private const string DualSenseMapping =
        "050000004c050000e60c000000810000,PS5 Controller,a:b0,b:b1,back:b4,dpdown:b12,dpleft:b13,dpright:b14,dpup:b11," +
        "guide:b5,leftshoulder:b9,leftstick:b7,lefttrigger:a4,leftx:a0,lefty:a1,misc1:b15,rightshoulder:b10,rightstick:b8," +
        "righttrigger:a5,rightx:a2,righty:a3,start:b6,x:b2,y:b3,touchpad:b16,platform:Windows,";

    private static readonly ControllerDescriptor DualSense = new()
    {
        Key = "ds5", Name = "DualSense Wireless Controller", Kind = ControllerKind.PlayStation5,
        SdlGuid = "050012344C050000E60C000000810000", SdlMapping = DualSenseMapping, SdlIndex = 1,
    };

    private static InputSetup Setup(ControllerDescriptor device, EmulatedPad pad, params ButtonRemap[] remap)
    {
        var setup = new InputSetup { Remap = remap };
        setup.Players.Add(new ResolvedPlayer(1, device, PlayerMode.Native, pad, "test"));
        return setup;
    }

    [Fact]
    public void MelonDS_writer_maps_dualsense_by_position_with_stick_on_dpad_and_keyboard_defaults()
    {
        using var hub = new TempHub();
        var file = hub.File("melonDS.toml", Encoding.UTF8.GetBytes(
            "[Instance0]\nJoystickID = 0\n\n[Instance0.Keyboard]\nA = -1\nB = 81\n\n[Instance0.Joystick]\nA = -1\n"));
        var toml = MelonToml.Load(file);
        MelonDsInputWriter.Write(toml, Setup(DualSense, EmulatedPad.NintendoDS));
        toml.Save(file);
        toml = MelonToml.Load(file);

        Assert.Equal(1, toml.GetInt("Instance0", "JoystickID"));
        Assert.Equal(1, toml.GetInt("Instance0.Joystick", "A"));   // rechte Taste (Kreis) = DS-A
        Assert.Equal(0, toml.GetInt("Instance0.Joystick", "B"));   // untere Taste (Kreuz) = DS-B
        Assert.Equal(4, toml.GetInt("Instance0.Joystick", "Select"));
        // Steuerkreuz ↑: Taste 11 + linker Stick Achse 1 negativ
        Assert.Equal(11 | 0x10000 | (1 << 24) | (1 << 20), toml.GetInt("Instance0.Joystick", "Up"));
        Assert.Equal(14 | 0x10000 | (0 << 24) | (0 << 20), toml.GetInt("Instance0.Joystick", "Right"));
        Assert.Equal(0x58, toml.GetInt("Instance0.Keyboard", "A")); // fehlende Tastaturbelegung ergänzt
        Assert.Equal(81, toml.GetInt("Instance0.Keyboard", "B"));   // eigene Belegung bleibt
    }

    [Fact]
    public void MelonDS_writer_applies_remap_and_encodes_hats_and_triggers()
    {
        Assert.Equal(0x100 | (0 << 4) | 4, MelonDsInputWriter.ButtonValue("h0.4"));
        Assert.Equal(0xFFFF | 0x10000 | (4 << 24) | (2 << 20), MelonDsInputWriter.ButtonValue("a4"));
        var toml = MelonToml.Load("does-not-exist.toml");
        MelonDsInputWriter.Write(toml, Setup(DualSense, EmulatedPad.NintendoDS,
            new ButtonRemap { Pad = EmulatedPad.NintendoDS, Target = "L", Source = PadButton.L2 }));
        Assert.Equal(0xFFFF | 0x10000 | (4 << 24) | (2 << 20), toml.GetInt("Instance0.Joystick", "L"));
    }

    [Fact]
    public void Azahar_writer_uses_controller_api_with_maptype_all_in_active_profile()
    {
        using var hub = new TempHub();
        var file = hub.File("qt-config.ini", Encoding.UTF8.GetBytes(
            "[Controls]\nprofile\\default=true\nprofile=0\nprofiles\\1\\name=Default\n" +
            "profiles\\1\\button_a\\default=true\nprofiles\\1\\button_a=\"code:65,engine:keyboard\"\nprofiles\\size=1\n"));
        var ini = EdenIni.Load(file);
        AzaharInputWriter.Write(ini, Setup(DualSense, EmulatedPad.Nintendo3DS));
        ini.Save(file);
        ini = EdenIni.Load(file);

        var dev = "engine:sdl,api:controller,maptype:all,guid:050012344c050000e60c000000810000,port:0";
        Assert.Equal(dev + ",button:1", ini.Get("Controls", @"profiles\1\button_a"));
        Assert.Equal(dev + ",button:0", ini.Get("Controls", @"profiles\1\button_b"));
        Assert.Equal(dev + ",axis:4,direction:+,threshold:0.500000", ini.Get("Controls", @"profiles\1\button_zl"));
        Assert.Equal(dev + ",axis_x:0,axis_y:1,deadzone:0.100000", ini.Get("Controls", @"profiles\1\circle_pad"));
        Assert.Equal("false", ini.Get("Controls", @"profiles\1\button_a\default"));

        AzaharInputWriter.Write(ini, Setup(ControllerDescriptor.Keyboard, EmulatedPad.Nintendo3DS));
        Assert.Null(ini.Get("Controls", @"profiles\1\button_a"));
        Assert.Equal("true", ini.Get("Controls", @"profiles\1\button_a\default"));
    }

    [Fact]
    public void Scanner_reads_3ds_title_id_from_ncsd_header()
    {
        using var hub = new TempHub();
        var rom = new byte[0x200];
        Encoding.ASCII.GetBytes("NCSD").CopyTo(rom, 0x100);
        BitConverter.GetBytes(0x0004000000030800UL).CopyTo(rom, 0x108);
        hub.File("lib/Mario Kart 7.3ds", rom);
        var result = new GameScanner().Scan([new ScanRoot(Path.Combine(hub.Root, "lib"), HubPlatform.ThreeDS)], TestContext.Current.CancellationToken);
        Assert.Equal("0004000000030800", Assert.Single(result.Games).GameCode);
    }

    [Fact]
    public void MelonDS_launch_sets_per_game_save_folder_and_adopts_existing_save()
    {
        using var hub = new TempHub();
        hub.File("integrations/melonds/melonDS-1.1/melonDS.exe");
        var game = hub.File("games/Mario Kart DS.nds");
        hub.File("games/Mario Kart DS.sav", [1, 2, 3]);
        var melon = new MelonDsAdapter(hub.Paths, hub.Config);
        var entry = new GameEntry { Id = "ds:AMCP", Title = "MKDS", Path = game, GameCode = "AMCP", Platform = HubPlatform.DS };

        melon.PrepareLaunch(new LaunchRequest { Game = entry });

        var dir = melon.SaveDirectory(entry)!;
        Assert.EndsWith(Path.Combine("saves", "AMCP"), dir);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(dir, "Mario Kart DS.sav")));
        Assert.Equal(dir, MelonToml.Load(melon.ConfigFile()!).Get("Instance0", "SaveFilePath"));
    }

    [Fact]
    public void Registry_maps_handheld_platforms_to_their_adapters()
    {
        using var hub = new TempHub();
        var registry = new AdapterRegistry(hub.Paths, hub.Config, hub.Backups);
        Assert.IsType<MelonDsAdapter>(registry.ForGame(new GameEntry { Id = "a", Title = "a", Path = "a.nds", Platform = HubPlatform.DS }));
        Assert.IsType<AzaharAdapter>(registry.ForGame(new GameEntry { Id = "b", Title = "b", Path = "b.3ds", Platform = HubPlatform.ThreeDS }));
    }
}
