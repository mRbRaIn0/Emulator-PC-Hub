using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Input;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.Tests;

/// <summary>Die Belegungsanzeige muss genau das zeigen, was die Input-Writer in die Emulatoren schreiben.</summary>
public class ButtonMapTests
{
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

    // Dolphin-SDL-Eingabenamen je Gamepad-Taste
    private static readonly Dictionary<PadButton, string> DolphinSdl = new()
    {
        [PadButton.South] = "`Button S`", [PadButton.East] = "`Button E`", [PadButton.West] = "`Button W`", [PadButton.North] = "`Button N`",
        [PadButton.L1] = "`Shoulder L`", [PadButton.R1] = "`Shoulder R`", [PadButton.L2] = "`Trigger L`", [PadButton.R2] = "`Trigger R`",
        [PadButton.L3] = "`Thumb L`", [PadButton.R3] = "`Thumb R`", [PadButton.Back] = "Back", [PadButton.Start] = "Start", [PadButton.Guide] = "Guide",
        [PadButton.DUp] = "`Pad N`", [PadButton.DDown] = "`Pad S`", [PadButton.DLeft] = "`Pad W`", [PadButton.DRight] = "`Pad E`",
    };

    // Ziel in ButtonMap → Schlüssel in der Dolphin-INI
    private static readonly Dictionary<string, string> WiimoteKeys = new()
    {
        ["A"] = "Buttons/A", ["B"] = "Buttons/B", ["1"] = "Buttons/1", ["2"] = "Buttons/2", ["−"] = "Buttons/-", ["+"] = "Buttons/+",
        ["Home"] = "Buttons/Home", ["Steuerkreuz ↑"] = "D-Pad/Up", ["Steuerkreuz ↓"] = "D-Pad/Down", ["Steuerkreuz ←"] = "D-Pad/Left",
        ["Steuerkreuz →"] = "D-Pad/Right", ["Nunchuk C"] = "Nunchuk/Buttons/C", ["Nunchuk Z"] = "Nunchuk/Buttons/Z",
        ["Wii Remote schütteln"] = "Shake/X", ["Nunchuk schütteln"] = "Nunchuk/Shake/X",
    };

    private static readonly Dictionary<string, string> GameCubeKeys = new()
    {
        ["A"] = "Buttons/A", ["B"] = "Buttons/B", ["X"] = "Buttons/X", ["Y"] = "Buttons/Y", ["Z"] = "Buttons/Z", ["Start/Pause"] = "Buttons/Start",
        ["L"] = "Triggers/L", ["R"] = "Triggers/R", ["Control-Stick langsam"] = "Main Stick/Modifier",
        ["Steuerkreuz ↑"] = "D-Pad/Up", ["Steuerkreuz ↓"] = "D-Pad/Down", ["Steuerkreuz ←"] = "D-Pad/Left", ["Steuerkreuz →"] = "D-Pad/Right",
    };

    private static Dictionary<string, string> DolphinIni(ControllerDescriptor device, EmulatedPad pad, string file, string section,
        List<ButtonRemap>? remap = null)
    {
        using var hub = new TempHub();
        var setup = new InputSetup { Remap = remap ?? [] };
        setup.Players.Add(new ResolvedPlayer(1, device, PlayerMode.Native, pad, "test"));
        new DolphinInputWriter(Path.Combine(hub.Root, "dolphin"), null).Apply(setup, new GameEntry { Id = "g", Title = "Test", Platform = HubPlatform.Wii });
        var lines = File.ReadAllLines(Path.Combine(hub.Root, "dolphin", "Config", file));
        var start = Array.IndexOf(lines, $"[{section}]");
        return lines.Skip(start + 1).TakeWhile(l => !l.StartsWith('['))
            .Where(l => l.Contains(" = ")).Select(l => l.Split(" = ", 2)).ToDictionary(p => p[0], p => p[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dolphin_wiimote_table_matches_written_config(bool nintendo)
    {
        var device = nintendo ? SwitchPro : DualSense;
        var ini = DolphinIni(device, EmulatedPad.Wiimote, "WiimoteNew.ini", "Wiimote1");
        var rows = ButtonMap.For(EmulatedPad.Wiimote, nintendo);
        foreach (var row in rows)
        {
            if (row.Source is not { } src || !DolphinSdl.ContainsKey(src))
                continue;
            Assert.Equal(DolphinSdl[src], ini[WiimoteKeys[row.Target]]);
        }
        // Nicht belegte Tasten dürfen in der INI nirgends als Wii-Taste auftauchen
        foreach (var b in ButtonMap.Unused(rows).Where(DolphinSdl.ContainsKey))
            Assert.DoesNotContain(DolphinSdl[b], ini.Where(kv => kv.Key != "Device").Select(kv => kv.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dolphin_gamecube_table_matches_written_config(bool nintendo)
    {
        var device = nintendo ? SwitchPro : DualSense;
        var ini = DolphinIni(device, EmulatedPad.GameCube, "GCPadNew.ini", "GCPad1");
        foreach (var row in ButtonMap.For(EmulatedPad.GameCube, nintendo))
        {
            if (row.Source is not { } src || !DolphinSdl.ContainsKey(src) || !GameCubeKeys.TryGetValue(row.Target, out var key))
                continue;
            Assert.Equal(DolphinSdl[src], ini[key]);
        }
    }

    [Fact]
    public void Cemu_table_matches_written_profile()
    {
        // SDL-Gamepad-Tastenindex je Gamepad-Taste (so schreibt Cemu SDLController-Profile)
        var sdlIndex = new Dictionary<PadButton, int>
        {
            [PadButton.South] = 0, [PadButton.East] = 1, [PadButton.West] = 2, [PadButton.North] = 3, [PadButton.Back] = 4, [PadButton.Guide] = 5,
            [PadButton.Start] = 6, [PadButton.L3] = 7, [PadButton.R3] = 8, [PadButton.L1] = 9, [PadButton.R1] = 10,
            [PadButton.DUp] = 11, [PadButton.DDown] = 12, [PadButton.DLeft] = 13, [PadButton.DRight] = 14,
        };
        // Wii-U-Pro-Mapping-IDs (CemuInputWriter)
        var cemuId = new Dictionary<string, int>
        {
            ["A"] = 1, ["B"] = 2, ["X"] = 3, ["Y"] = 4, ["L"] = 5, ["R"] = 6, ["+"] = 9, ["−"] = 10, ["Home"] = 11,
            ["Steuerkreuz ↑"] = 12, ["Steuerkreuz ↓"] = 13, ["Steuerkreuz ←"] = 14, ["Steuerkreuz →"] = 15,
            ["Linken Stick drücken"] = 16, ["Rechten Stick drücken"] = 17,
        };
        var doc = CemuInputWriter.BuildProfile(new ResolvedPlayer(1, DualSense, PlayerMode.Native, EmulatedPad.WiiUPro, "test"), true);
        var entries = doc.Descendants("entry").ToDictionary(e => (int)e.Element("mapping")!, e => (int)e.Element("button")!);
        foreach (var row in ButtonMap.For(EmulatedPad.WiiUPro, false))
        {
            if (row.Source is not { } src || !sdlIndex.ContainsKey(src) || !cemuId.TryGetValue(row.Target, out var id))
                continue;
            Assert.Equal(sdlIndex[src], entries[id]);
        }
    }

    [Fact]
    public void Dolphin_free_slots_accept_real_wiimotes_when_enabled()
    {
        using var hub = new TempHub();
        var dir = Path.Combine(hub.Root, "dolphin");
        var game = new GameEntry { Id = "g", Title = "Test", Platform = HubPlatform.Wii };
        var setup = new InputSetup { RealWiimotesInFreeSlots = true };
        setup.Players.Add(new ResolvedPlayer(1, DualSense, PlayerMode.Native, EmulatedPad.Wiimote, "test"));
        new DolphinInputWriter(dir, null).Apply(setup, game);

        var lines = File.ReadAllLines(Path.Combine(dir, "Config", "WiimoteNew.ini"));
        string SourceOf(int p) => lines.SkipWhile(l => l != $"[Wiimote{p}]").Skip(1).First(l => l.StartsWith("Source"));
        Assert.Equal("Source = 1", SourceOf(1)); // PS5 = emulierte Wii Remote
        Assert.Equal("Source = 2", SourceOf(2)); // frei = echte Wii Remote
        Assert.Equal("Source = 2", SourceOf(4));
        Assert.Contains("WiimoteContinuousScanning = True", File.ReadAllText(Path.Combine(dir, "Config", "Dolphin.ini")));

        var off = new InputSetup();
        off.Players.Add(new ResolvedPlayer(1, DualSense, PlayerMode.Native, EmulatedPad.Wiimote, "test"));
        new DolphinInputWriter(dir, null).Apply(off, game);
        lines = File.ReadAllLines(Path.Combine(dir, "Config", "WiimoteNew.ini"));
        Assert.Equal("Source = 0", SourceOf(2));
    }

    [Fact]
    public void Ps5_labels_use_playstation_names()
    {
        Assert.Equal("✕ Kreuz", ButtonMap.Label(PadButton.South, ControllerKind.PlayStation5));
        Assert.Equal("R2", ButtonMap.Label(PadButton.R2, ControllerKind.PlayStation5));
        Assert.Equal("A", ButtonMap.Label(PadButton.East, ControllerKind.SwitchPro));
        // PS5 ✕ ist am Wii Remote „A“, in Cemu/Eden aber „B“ (Belegung nach Position)
        Assert.Equal("A", ButtonMap.For(EmulatedPad.Wiimote, false).Single(r => r.Source == PadButton.South).Target);
        Assert.Equal("B", ButtonMap.For(EmulatedPad.SwitchPro, false).Single(r => r.Source == PadButton.South).Target);
    }

    [Fact]
    public void Assign_swaps_occupied_buttons_and_reset_restores_both()
    {
        var remap = new List<ButtonRemap>();
        // Wii Remote: A liegt auf ✕, B auf R2 → A auf R2 legen tauscht die beiden
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "A", PadButton.R2);
        var rows = ButtonMap.For(EmulatedPad.Wiimote, false, remap);
        Assert.Equal(PadButton.R2, rows.Single(r => r.Target == "A").Source);
        Assert.Equal(PadButton.South, rows.Single(r => r.Target == "B").Source);
        Assert.Equal(2, remap.Count);

        // Freie Taste (△) zuweisen: kein Tausch, 1 → △; ✕ bleibt bei B
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "1", PadButton.North);
        rows = ButtonMap.For(EmulatedPad.Wiimote, false, remap);
        Assert.Equal(PadButton.North, rows.Single(r => r.Target == "1").Source);
        Assert.Contains(PadButton.West, ButtonMap.Unused(rows));

        // Nicht belegen
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "Home", null);
        Assert.Null(ButtonMap.For(EmulatedPad.Wiimote, false, remap).Single(r => r.Target == "Home").Source);

        // Zurück auf Standard: A bekommt ✕ wieder, B bekommt R2 (Tausch rückgängig)
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "A", PadButton.South);
        rows = ButtonMap.For(EmulatedPad.Wiimote, false, remap);
        Assert.False(rows.Single(r => r.Target == "A").Changed);
        Assert.False(rows.Single(r => r.Target == "B").Changed);

        // Sticks sind fest; Umbelegung gilt nur für den eigenen Controller-Typ
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "Nunchuk-Stick", PadButton.South);
        Assert.Equal(PadButton.LeftStick, ButtonMap.For(EmulatedPad.Wiimote, false, remap).Single(r => r.Target == "Nunchuk-Stick").Source);
        Assert.DoesNotContain(ButtonMap.For(EmulatedPad.GameCube, false, remap), r => r.Changed);

        ButtonMap.Reset(remap, EmulatedPad.Wiimote);
        Assert.Empty(remap);
    }

    [Fact]
    public void Remap_is_written_to_dolphin_and_cemu()
    {
        var remap = new List<ButtonRemap>();
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "A", PadButton.R2);   // A ↔ B tauschen
        ButtonMap.Assign(remap, EmulatedPad.Wiimote, false, "Home", null);        // Home nicht belegen
        var wm = DolphinIni(DualSense, EmulatedPad.Wiimote, "WiimoteNew.ini", "Wiimote1", remap);
        Assert.Equal("`Trigger R`", wm["Buttons/A"]);
        Assert.Equal("`Button S`", wm["Buttons/B"]);
        Assert.Equal("", wm["Buttons/Home"]);

        var gcRemap = new List<ButtonRemap>();
        ButtonMap.Assign(gcRemap, EmulatedPad.GameCube, false, "L", PadButton.L1);
        var gc = DolphinIni(DualSense, EmulatedPad.GameCube, "GCPadNew.ini", "GCPad1", gcRemap);
        Assert.Equal("`Shoulder L`", gc["Triggers/L"]);
        Assert.Equal("`Shoulder L`", gc["Triggers/L-Analog"]);

        // Cemu: Wii-U-A (Standard ○) auf ✕ legen → A = SDL 0, B (vorher ✕) = SDL 1
        var cemuRemap = new List<ButtonRemap>();
        ButtonMap.Assign(cemuRemap, EmulatedPad.WiiUPro, false, "A", PadButton.South);
        var doc = CemuInputWriter.BuildProfile(new ResolvedPlayer(1, DualSense, PlayerMode.Native, EmulatedPad.WiiUPro, "test"), true, cemuRemap);
        var entries = doc.Descendants("entry").ToDictionary(e => (int)e.Element("mapping")!, e => (int)e.Element("button")!);
        Assert.Equal(0, entries[1]);
        Assert.Equal(1, entries[2]);
    }

    [Fact]
    public void Game_controls_are_per_game_and_pad_and_override_suggestions()
    {
        using var hub = new TempHub();
        var store = new GameControlsStore(hub.Paths);
        var mkwii = new GameEntry { Id = "mk", Title = "Mario Kart Wii", Platform = HubPlatform.Wii, Special = SpecialPage.MarioKartWii };

        Assert.Equal(("Gas geben", true), store.Get(mkwii, EmulatedPad.Wiimote, "A"));
        Assert.Equal("Bremsen / rückwärts", store.Get(mkwii, EmulatedPad.GameCube, "B").text);

        store.Set(mkwii, EmulatedPad.Wiimote, "Nunchuk Z", "Item benutzen");
        store.Set(mkwii, EmulatedPad.Wiimote, "A", "");
        var reloaded = new GameControlsStore(hub.Paths);
        Assert.Equal(("Item benutzen", false), reloaded.Get(mkwii, EmulatedPad.Wiimote, "Nunchuk Z"));
        Assert.Equal((null, false), reloaded.Get(mkwii, EmulatedPad.Wiimote, "A"));
        Assert.Null(reloaded.Get(mkwii, EmulatedPad.GameCube, "Nunchuk Z").text);

        reloaded.Reset(mkwii, EmulatedPad.Wiimote, "A");
        Assert.Equal(("Gas geben", true), reloaded.Get(mkwii, EmulatedPad.Wiimote, "A"));
    }

    [Fact]
    public void Mario_kart_double_dash_gas_on_r2_keeps_cross_for_menus()
    {
        var remap = new List<ButtonRemap>();
        ButtonMap.Assign(remap, EmulatedPad.GameCube, false, "A", PadButton.R2);
        ButtonMap.Assign(remap, EmulatedPad.GameCube, false, "R", PadButton.R1);
        ButtonMap.Assign(remap, EmulatedPad.GameCube, false, "Z", PadButton.L1);
        var gc = DolphinIni(DualSense, EmulatedPad.GameCube, "GCPadNew.ini", "GCPad1", remap);
        Assert.Equal("`Trigger R` | `Button S`", gc["Buttons/A"]); // Gas auf R2, ✕ bestätigt weiter
        Assert.Equal("`Shoulder R`", gc["Triggers/R"]);
        Assert.Equal("`Shoulder L`", gc["Buttons/Z"]);
    }

    [Fact]
    public void Confirm_with_cross_only_for_non_nintendo_controllers()
    {
        var remap = new List<ButtonRemap>();
        ButtonMap.ApplyConfirmSouth(remap, true);
        var ps = ButtonMap.For(EmulatedPad.SwitchPro, false, remap);
        Assert.Equal(PadButton.South, ps.Single(r => r.Target == "A").Source);
        Assert.Equal(PadButton.East, ps.Single(r => r.Target == "B").Source);
        var nintendo = ButtonMap.For(EmulatedPad.SwitchPro, true, remap);
        Assert.Equal(PadButton.East, nintendo.Single(r => r.Target == "A").Source);
        ButtonMap.ApplyConfirmSouth(remap, false);
        Assert.Empty(remap);
    }
}

public class DeviceAssignmentTests
{
    private static ControllerDescriptor Pad(string key) => new() { Key = key, Name = "DualSense Wireless Controller", Kind = ControllerKind.PlayStation5 };

    private static readonly ControllerDescriptor Mine = Pad("ds:e8");
    private static readonly ControllerDescriptor Friend = Pad("ds:bc");

    [Fact]
    public void Fixed_device_on_other_slot_is_never_used_twice()
    {
        // Standardprofil: Spieler 1 fest = mein Controller, Spieler 2 automatisch – mein Controller liegt aber auf Platz 2
        var profile = new ControllerProfile();
        profile.For(1).DeviceKey = Mine.Key;
        var slots = new Dictionary<int, ControllerDescriptor> { [1] = Friend, [2] = Mine };

        var result = UI.Services.ControllerCoordinator.AssignDevices(profile, slots, [Friend, Mine]);

        Assert.Equal(Mine, result[1]);
        Assert.Equal(Friend, result[2]); // das freie Gerät rückt nach statt meinen Controller doppelt zu nehmen
        Assert.Equal(2, result.Values.Select(d => d.Key).Distinct().Count());
    }

    [Fact]
    public void Automatic_profile_follows_player_slots_and_keyboard_only_fills_empty_player_one()
    {
        var profile = new ControllerProfile();
        var slots = new Dictionary<int, ControllerDescriptor> { [1] = Mine, [2] = Friend };
        var result = UI.Services.ControllerCoordinator.AssignDevices(profile, slots, [Mine, Friend]);
        Assert.Equal(Mine, result[1]);
        Assert.Equal(Friend, result[2]);
        Assert.DoesNotContain(result.Values, d => d.IsKeyboard);

        var empty = UI.Services.ControllerCoordinator.AssignDevices(profile, new Dictionary<int, ControllerDescriptor>(), []);
        Assert.True(empty[1].IsKeyboard);
    }

    [Fact]
    public void Same_fixed_device_on_two_players_counts_once()
    {
        var profile = new ControllerProfile();
        profile.For(1).DeviceKey = Friend.Key;
        profile.For(2).DeviceKey = Friend.Key;
        var slots = new Dictionary<int, ControllerDescriptor> { [1] = Mine, [2] = Friend };
        var result = UI.Services.ControllerCoordinator.AssignDevices(profile, slots, [Mine, Friend]);
        Assert.Equal(Friend, result[1]);
        Assert.Equal(Mine, result[2]);
    }

}

