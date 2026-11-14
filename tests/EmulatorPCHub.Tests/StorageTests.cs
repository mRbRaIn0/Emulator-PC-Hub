using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;

namespace EmulatorPCHub.Tests;

public class StorageTests
{
    [Fact]
    public void Defaults_are_local_and_an_overridden_local_folder_is_used()
    {
        using var hub = new TempHub();
        var standard = new AppPaths(hub.Root);
        Assert.Equal(Path.Combine(hub.Root, "data"), standard.Data);
        Assert.Null(standard.Notice);

        var other = Path.Combine(hub.Root, "elsewhere");
        var moved = new AppPaths(hub.Root, new PathOverrides { Data = other, Backups = Path.Combine(other, "b") });
        Assert.Equal(other, moved.Data);
        Assert.Equal(Path.Combine(other, "b"), moved.Backups);
        Assert.Equal(Path.Combine(hub.Root, "Logs"), moved.Logs); // nicht gesetzt: Standard
        Assert.Equal(Path.Combine(other, "library.db"), moved.LibraryDb); // abgeleitete Pfade folgen
    }

    [Fact]
    public void Unreachable_storage_falls_back_to_the_local_default_with_a_notice()
    {
        using var hub = new TempHub();
        var paths = new AppPaths(hub.Root, new PathOverrides { Data = @"\\nas-gibt-es-nicht.invalid\Freigabe\data", Logs = @"Q:\gibt\es\nicht" });
        Assert.Equal(Path.Combine(hub.Root, "data"), paths.Data);
        Assert.Equal(Path.Combine(hub.Root, "Logs"), paths.Logs);
        Assert.Contains("Hub-Daten", paths.Notice);
    }

    [Fact]
    public void Overrides_roundtrip_through_paths_json_and_a_broken_file_means_defaults()
    {
        using var hub = new TempHub();
        new PathOverrides { Data = @"\\nas\x\data", NasHosts = ["nas", "100.1.2.3"] }.Save(hub.Root);
        var loaded = PathOverrides.Load(hub.Root);
        Assert.Equal(@"\\nas\x\data", loaded.Data);
        Assert.Equal(["nas", "100.1.2.3"], loaded.NasHosts);

        File.WriteAllText(PathOverrides.FileFor(hub.Root), "{ kaputt");
        Assert.Equal("", PathOverrides.Load(hub.Root).Data);
    }

    [Fact]
    public void Copy_folder_keeps_existing_files_in_the_target()
    {
        using var hub = new TempHub();
        hub.File("a/x.txt", [1]);
        hub.File("a/sub/y.txt", [2]);
        hub.File("b/x.txt", [9]);
        PathOverrides.CopyFolder(Path.Combine(hub.Root, "a"), Path.Combine(hub.Root, "b"));
        Assert.Equal([9], File.ReadAllBytes(Path.Combine(hub.Root, "b", "x.txt")));
        Assert.Equal([2], File.ReadAllBytes(Path.Combine(hub.Root, "b", "sub", "y.txt")));
    }

    [Theory]
    [InlineData("", "desktop", "maximized")]
    [InlineData("", "console", "fullscreen")]
    [InlineData("windowed", "console", "windowed")]
    [InlineData("quatsch", "desktop", "maximized")]
    public void Window_mode_falls_back_to_the_old_startup_mode(string mode, string startup, string expected) =>
        Assert.Equal(expected, new UiConfig { WindowMode = mode, StartupMode = startup }.EffectiveWindowMode);

    [Fact]
    public void Look_defaults_are_gray_without_animation() =>
        Assert.Equal(("grau", false), (new UiConfig().BackgroundColor, new UiConfig().BackgroundAnimation));
}
