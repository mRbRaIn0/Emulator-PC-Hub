using System.Text.Json.Nodes;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Emulation.WheelWizard;
using EmulatorPCHub.Emulation.WiiCompiled;
using EmulatorPCHub.Mods;

namespace EmulatorPCHub.Tests;

public class EmulationTests
{
    private static (TempHub hub, DolphinAdapter dolphin, string game) SetupDolphinWithRetroRewind()
    {
        var hub = new TempHub();
        hub.File("integrations/dolphin/Dolphin-x64/Dolphin.exe");
        hub.File("integrations/dolphin/Dolphin-x64/portable.txt");
        var user = Path.Combine(hub.Root, "integrations/dolphin/Dolphin-x64/User");
        hub.File("integrations/dolphin/Dolphin-x64/User/Load/Riivolution/WheelWizard/riivolution/RetroRewind6.xml");
        hub.File("integrations/dolphin/Dolphin-x64/User/Load/Riivolution/WheelWizard/RetroRewind6/version.txt", "6.12.8"u8.ToArray());
        var game = hub.File("Library/Wii/mkwii.iso", Fake.DiscHeader("RMCP01", "MARIO KART Wii", true));
        var ww = new WheelWizardIntegration(hub.Paths, hub.Config);
        return (hub, new DolphinAdapter(hub.Paths, hub.Config, ww), game);
    }

    [Fact]
    public void Dolphin_is_detected_portable_with_retro_rewind()
    {
        var (hub, dolphin, _) = SetupDolphinWithRetroRewind();
        using (hub)
        {
            var inst = dolphin.DetectInstallation();
            Assert.True(inst.IsInstalled);
            Assert.True(inst.Portable);
            var rr = RetroRewindLayout.Find(inst.UserDataDir!);
            Assert.NotNull(rr);
            Assert.Equal("6.12.8", rr.Version);
        }
    }

    [Fact]
    public void Dolphin_retro_rewind_launch_uses_mod_descriptor()
    {
        var (hub, dolphin, game) = SetupDolphinWithRetroRewind();
        using (hub)
        {
            var entry = new GameEntry { Id = KnownGames.MarioKartWiiId, Title = "Mario Kart Wii", Platform = HubPlatform.Wii, Path = game };
            var preset = PresetService.MarioKartWiiDefaults.First(p => p.Kind == PresetKind.RetroRewind);
            var spec = dolphin.PrepareLaunch(new LaunchRequest { Game = entry, Preset = preset, Fullscreen = true });

            Assert.Equal("-b", spec.Arguments[0]);
            Assert.Equal("-e", spec.Arguments[1]);
            var descriptor = JsonNode.Parse(File.ReadAllText(spec.Arguments[2]))!;
            Assert.Equal("dolphin-game-mod-descriptor", descriptor["type"]!.GetValue<string>());
            Assert.Equal(Path.GetFullPath(game), descriptor["base-file"]!.GetValue<string>());
            var patch = descriptor["riivolution"]!["patches"]![0]!;
            Assert.EndsWith("RetroRewind6.xml", patch["xml"]!.GetValue<string>());
            Assert.Equal("Pack", patch["options"]![0]!["option-name"]!.GetValue<string>());
            Assert.Contains("Dolphin.Display.Fullscreen=True", spec.Arguments);
        }
    }

    [Fact]
    public void Dolphin_vanilla_launch_uses_game_directly()
    {
        var (hub, dolphin, game) = SetupDolphinWithRetroRewind();
        using (hub)
        {
            var entry = new GameEntry { Id = "x", Title = "Mario Kart Wii", Platform = HubPlatform.Wii, Path = game };
            var spec = dolphin.PrepareLaunch(new LaunchRequest { Game = entry, Preset = PresetService.MarioKartWiiDefaults[0], Fullscreen = false });
            Assert.Equal(game, spec.Arguments[2]);
            Assert.DoesNotContain("Dolphin.Display.Fullscreen=True", spec.Arguments);
            Assert.Contains("Dolphin.Core.WiiKeyboard=True", spec.Arguments);
        }
    }

    [Fact]
    public void Dolphin_skylanders_launch_emulates_portal_and_gamecube_has_no_usb_keyboard()
    {
        var (hub, dolphin, game) = SetupDolphinWithRetroRewind();
        using (hub)
        {
            var sky = new GameEntry { Id = "wii:SKYP52", Title = "Skylanders Giants", Platform = HubPlatform.Wii, Path = game };
            var spec = dolphin.PrepareLaunch(new LaunchRequest { Game = sky });
            Assert.Contains("Dolphin.EmulatedUSBDevices.EmulateSkylanderPortal=True", spec.Arguments);
            Assert.Contains("Dolphin.Input.BackgroundInput=True", spec.Arguments);
            Assert.NotNull(spec.AfterStart);

            var gc = new GameEntry { Id = "gc:x", Title = "Mario Kart Double Dash", Platform = HubPlatform.GameCube, Path = game };
            var gcSpec = dolphin.PrepareLaunch(new LaunchRequest { Game = gc });
            Assert.DoesNotContain("Dolphin.Core.WiiKeyboard=True", gcSpec.Arguments);
            Assert.Null(gcSpec.AfterStart);
        }
    }

    [Theory]
    [InlineData("SetupVersion")]
    [InlineData("setupVersion")]
    public void WiiCompiled_uses_official_launch_arguments(string versionProperty)
    {
        using var hub = new TempHub();
        hub.File("integrations/wiicompiled/Install/WiiCompiled-Setup.exe");
        hub.File("integrations/wiicompiled/Install/install-state.json", System.Text.Encoding.UTF8.GetBytes($"{{ \"{versionProperty}\": \"0.2.32\" }}"));
        var adapter = new WiiCompiledAdapter(hub.Paths, hub.Config, new WheelWizardIntegration(hub.Paths, hub.Config));
        var install = adapter.FindInstall();
        Assert.NotNull(install);
        Assert.Equal("0.2.32", install.Version);

        var game = new GameEntry { Id = KnownGames.MarioKartWiiId, Title = "Mario Kart Wii", Platform = HubPlatform.Wii };
        var retro = adapter.PrepareLaunch(new LaunchRequest { Game = game, Preset = PresetService.MarioKartWiiDefaults[1] });
        Assert.Equal(["--launch-retro"], retro.Arguments);
        var vanilla = adapter.PrepareLaunch(new LaunchRequest { Game = game, Preset = PresetService.MarioKartWiiDefaults[0] });
        Assert.Equal(["--launch-base"], vanilla.Arguments);
    }

    [Fact]
    public void WiiCompiled_requires_pal_dump()
    {
        Assert.NotNull(WiiCompiledAdapter.ValidateDump(null));
        using var hub = new TempHub();
        var usa = hub.File("rmce.iso", new byte[4]);
        Assert.Contains("PAL", WiiCompiledAdapter.ValidateDump(new GameEntry { Id = "a", Title = "x", Path = usa, GameCode = "RMCE01" }));
        Assert.Null(WiiCompiledAdapter.ValidateDump(new GameEntry { Id = "a", Title = "x", Path = usa, GameCode = "RMCP01" }));
    }

    [Fact]
    public void WiiCompiled_progress_json_is_parsed()
    {
        var p = WiiCompiledAdapter.ParseProgress("""{"event":"progress","message":"Compiling","percent":42}""");
        Assert.Equal(42, p.Percent);
        Assert.Equal("Compiling", p.Message);
        Assert.Equal("plain text", WiiCompiledAdapter.ParseProgress("plain text").Message);
    }

    [Fact]
    public void Command_line_split_respects_quotes()
    {
        Assert.Equal(["-e", "C:\\Games\\Mario Kart.iso", "-b"], CommandLine.Split("-e \"C:\\Games\\Mario Kart.iso\" -b"));
    }

    [Fact]
    public void Ini_file_round_trip()
    {
        using var hub = new TempHub();
        var path = Path.Combine(hub.Root, "Dolphin.ini");
        var ini = new IniFile(path);
        ini.Set("General", "ISOPath0", @"D:\Wii");
        ini.Set("General", "ISOPaths", "1");
        ini.Set("Display", "Fullscreen", "True");
        ini.Save();
        var again = new IniFile(path);
        Assert.Equal(@"D:\Wii", again.Get("General", "ISOPath0"));
        Assert.Equal("True", again.Get("Display", "Fullscreen"));
    }

    private sealed class FakeAdapter(string exitCode) : IEmulatorAdapter
    {
        public string Id => "fake";
        public string DisplayName => "Fake";
        public IReadOnlyList<HubPlatform> Platforms => [HubPlatform.Wii];
        public EmulatorInstallation DetectInstallation() => new() { ExecutablePath = Environment.GetEnvironmentVariable("ComSpec") };
        public IReadOnlyList<string> DetectGames() => [];
        public LaunchSpec PrepareLaunch(LaunchRequest request) => new()
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec")!,
            Arguments = ["/c", "ping -n 2 127.0.0.1 >nul & exit " + exitCode],
        };
        public IReadOnlyDictionary<string, string> GetConfig() => new Dictionary<string, string>();
        public void ApplyConfig(IReadOnlyDictionary<string, string> values) { }
    }

    private sealed class FakeWindow : IHubWindow
    {
        public int Hidden, Restored;
        public void HideForGame() => Hidden++;
        public void RestoreAfterGame() => Restored++;
    }

    [Fact]
    public async Task Launch_pipeline_runs_all_steps_and_returns_to_hub()
    {
        using var hub = new TempHub();
        var pipeline = new LaunchPipeline(new LaunchLog(hub.Paths.Logs));
        var window = new FakeWindow();
        var steps = new List<LaunchStep>();
        var presetApplied = false;
        TimeSpan? recorded = null;
        var game = new GameEntry { Id = "t", Title = "Test", Platform = HubPlatform.Wii };

        var outcome = await pipeline.RunAsync(new LaunchRequest { Game = game }, new FakeAdapter("0"), window,
            new SyncProgress<LaunchProgress>(p => steps.Add(p.Step)),
            () => { presetApplied = true; return Task.CompletedTask; },
            (_, duration, _) => recorded = duration);

        Assert.True(outcome.Success, outcome.Error);
        Assert.Equal(0, outcome.ExitCode);
        Assert.True(presetApplied);
        Assert.NotNull(recorded);
        Assert.Equal(1, window.Hidden);
        Assert.Equal(1, window.Restored);
        Assert.Equal([LaunchStep.LoadPreset, LaunchStep.CheckFiles, LaunchStep.LoadControllerProfile, LaunchStep.ApplyPreset,
            LaunchStep.StartProcess, LaunchStep.HideHub, LaunchStep.Running, LaunchStep.SavePlaytime, LaunchStep.RestoreHub], steps);
        Assert.False(pipeline.IsRunning);
        Assert.Single(new LaunchLog(hub.Paths.Logs).ReadRecent());
    }

    [Fact]
    public async Task Launch_pipeline_reports_missing_emulator()
    {
        using var hub = new TempHub();
        var pipeline = new LaunchPipeline(new LaunchLog(hub.Paths.Logs));
        var ww = new WheelWizardIntegration(hub.Paths, hub.Config);
        var game = new GameEntry { Id = "t", Title = "Test", Platform = HubPlatform.Wii, Path = "nope.iso" };
        var window = new FakeWindow();
        var outcome = await pipeline.RunAsync(new LaunchRequest { Game = game }, new DolphinAdapter(hub.Paths, hub.Config, ww),
            window, null, null, null);
        Assert.False(outcome.Success);
        Assert.Contains("Dolphin", outcome.Error);
        Assert.Equal(0, window.Hidden);
    }
}

/// <summary>Progress ohne SynchronizationContext (deterministisch im Test).</summary>
public sealed class SyncProgress<T>(Action<T> action) : IProgress<T>
{
    public void Report(T value) => action(value);
}
