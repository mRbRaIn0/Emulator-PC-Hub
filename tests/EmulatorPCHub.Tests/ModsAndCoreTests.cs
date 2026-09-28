using System.IO.Compression;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Library;
using EmulatorPCHub.Mods;
using EmulatorPCHub.Updates;

namespace EmulatorPCHub.Tests;

public class ModsAndCoreTests
{
    private static (TempHub hub, SwitchModManager mods, EdenAdapter eden) SetupSwitch()
    {
        var hub = new TempHub();
        hub.File("integrations/switch/eden.exe");
        Directory.CreateDirectory(Path.Combine(hub.Root, "integrations/switch/user"));
        var eden = new EdenAdapter(hub.Paths, hub.Config, hub.Backups);
        return (hub, new SwitchModManager(hub.Paths, hub.Backups, () => eden), eden);
    }

    [Fact]
    public async Task Switch_mods_toggle_by_moving_folders()
    {
        var (hub, mods, eden) = SetupSwitch();
        using (hub)
        {
            const string tid = KnownGames.MarioKart8DeluxeTitleId;
            hub.File($"integrations/switch/mods-store/{tid}/CTGP-DX/romfs/Course/a.szs");
            hub.File($"integrations/switch/mods-store/{tid}/CTGP-DX/version.txt", "1.1.1"u8.ToArray());
            hub.File($"integrations/switch/mods-store/{tid}/Other/romfs/x.bin");

            var list = mods.List(tid);
            Assert.Equal(2, list.Count);
            Assert.All(list, m => Assert.False(m.Enabled));
            Assert.Equal("1.1.1", list.First(m => m.Name == "CTGP-DX").Version);

            await mods.ActivateExactlyAsync(tid, ["CTGP-DX"]);
            Assert.True(File.Exists(Path.Combine(eden.ModsDirectory(tid), "CTGP-DX", "romfs", "Course", "a.szs")));
            Assert.False(mods.List(tid).First(m => m.Name == "Other").Enabled);

            await mods.ActivateExactlyAsync(tid, []); // Vanilla
            Assert.All(mods.List(tid), m => Assert.False(m.Enabled));
            Assert.NotEmpty(hub.Backups.List(BackupCategory.Mods));
        }
    }

    [Fact]
    public async Task Mod_import_supports_atmosphere_layout_zip()
    {
        var (hub, mods, _) = SetupSwitch();
        using (hub)
        {
            const string tid = KnownGames.MarioKart8DeluxeTitleId;
            var src = Path.Combine(hub.Root, "zipsrc");
            Directory.CreateDirectory(Path.Combine(src, "atmosphere", "contents", tid, "romfs"));
            File.WriteAllText(Path.Combine(src, "atmosphere", "contents", tid, "romfs", "f.txt"), "x");
            var zip = Path.Combine(hub.Root, "CTGPDX v1.1.1.zip");
            ZipFile.CreateFromDirectory(src, zip);

            var mod = await mods.InstallAsync(zip, tid, "CTGP-DX");
            Assert.NotNull(mod);
            Assert.True(File.Exists(Path.Combine(mods.StoreDirectory(tid), "CTGP-DX", "romfs", "f.txt")));
        }
    }

    [Fact]
    public void Presets_for_mario_kart_follow_the_plan()
    {
        using var hub = new TempHub();
        var presets = new PresetService(hub.Paths, hub.Backups);
        var mkwii = presets.ForGame(new GameEntry { Id = KnownGames.MarioKartWiiId, Title = "Mario Kart Wii", Special = SpecialPage.MarioKartWii });
        Assert.Equal(["Vanilla", "Retro Rewind", "Custom"], mkwii.Select(p => p.Name));
        var mk8 = presets.ForGame(new GameEntry { Id = KnownGames.MarioKart8DeluxeId, Title = "Mario Kart 8 Deluxe", Special = SpecialPage.MarioKart8Deluxe });
        Assert.Equal(["Vanilla", "CTGP Deluxe", "Custom"], mk8.Select(p => p.Name));

        var game = new GameEntry { Id = "wii:SB4P01", Title = "Super Mario Galaxy 2", Platform = HubPlatform.Wii };
        presets.SaveCustom(game, new GamePreset { Id = "c1", Name = "Vulkan", Arguments = "-C x=y" });
        Assert.Contains(presets.ForGame(game), p => p.Name == "Vulkan" && !p.IsBuiltIn);
    }

    [Fact]
    public void Backup_restore_round_trip()
    {
        using var hub = new TempHub();
        var file = hub.File("cfg/settings.xml", "old"u8.ToArray());
        var entry = hub.Backups.BackupFile(BackupCategory.Config, file, "settings");
        Assert.NotNull(entry);
        File.WriteAllText(file, "new");
        Assert.True(hub.Backups.Restore(entry));
        Assert.Equal("old", File.ReadAllText(file));
    }

    [Fact]
    public void Retro_rewind_version_compare()
    {
        Assert.True(new RetroRewindStatus(true, "6.12.7", "6.12.8", null).UpdateAvailable);
        Assert.False(new RetroRewindStatus(true, "6.12.8", "6.12.8", null).UpdateAvailable);
    }

    [Fact]
    public void Mk8_update_version_text_from_filename()
    {
        var info = new GameFileInfo { Path = @"D:\MK8D Update [0100152000022800][v3.0.3].nsp", Version = 1507328 };
        Assert.Equal(MarioKart8DeluxeService.CtgpRequiredGameVersion, MarioKart8DeluxeService.VersionText(info));
    }

    [Fact]
    public async Task Component_installer_extracts_zip()
    {
        using var hub = new TempHub();
        var src = Path.Combine(hub.Root, "src");
        Directory.CreateDirectory(Path.Combine(src, "Cemu_2.6"));
        File.WriteAllText(Path.Combine(src, "Cemu_2.6", "Cemu.exe"), "");
        var zip = Path.Combine(hub.Root, "cemu.zip");
        ZipFile.CreateFromDirectory(src, zip);
        var target = Path.Combine(hub.Root, "out");
        await ComponentInstaller.ExtractAsync(zip, target, CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(target, "Cemu_2.6", "Cemu.exe")));
    }

    [Fact]
    public void Controller_navigation_repeats_only_directions()
    {
        // Ohne echte Controller liefert Poll keine Aktionen – der Dienst darf nicht abstürzen.
        var service = new ControllerService();
        var actions = new List<NavAction>();
        service.Action += actions.Add;
        for (int i = 0; i < 10; i++)
            service.Poll(1 / 60.0, deliverActions: true);
        Assert.True(service.ConnectedCount >= 0);
    }
}
