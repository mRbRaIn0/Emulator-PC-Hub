using System.IO.Compression;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Updates;

namespace EmulatorPCHub.Tests;

public class InstallFromFileTests
{
    private static string MakeZip(TempHub hub, string name, params string[] files)
    {
        var src = Path.Combine(hub.Root, "zip-" + Path.GetFileNameWithoutExtension(name));
        foreach (var f in files)
        {
            var p = Path.Combine(src, f);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, "x");
        }
        var zip = Path.Combine(hub.Root, name);
        ZipFile.CreateFromDirectory(src, zip);
        return zip;
    }

    [Fact]
    public async Task Source_code_archive_is_rejected_and_nothing_is_extracted()
    {
        using var hub = new TempHub();
        var zip = MakeZip(hub, "Website-main.zip", "Website-main/README.md", "Website-main/package.json");
        var installer = new ComponentInstaller(hub.Paths, hub.Backups);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            installer.InstallFromFileAsync(ComponentIds.Switch, zip, null, CancellationToken.None));
        Assert.Contains("eden.exe", ex.Message);
        Assert.False(Directory.Exists(Path.Combine(hub.Paths.IntegrationDir("switch"), "Website-main")));
    }

    [Fact]
    public async Task Eden_zip_is_installed_portable_and_keeps_user_data()
    {
        using var hub = new TempHub();
        var switchDir = hub.Paths.IntegrationDir("switch");
        // vorhandene Nutzerdaten (Keys) dürfen beim Update nicht verloren gehen
        var keys = Path.Combine(switchDir, "user", "keys", "prod.keys");
        Directory.CreateDirectory(Path.GetDirectoryName(keys)!);
        File.WriteAllText(keys, "meine-keys");

        var zip = MakeZip(hub, "Eden-Windows-v0.2.1-amd64-clang-pgo.zip", "eden.exe", "eden-cli.exe", "LICENSES/MIT.txt");
        var installer = new ComponentInstaller(hub.Paths, hub.Backups);
        var exe = await installer.InstallFromFileAsync(ComponentIds.Switch, zip, null, CancellationToken.None);

        Assert.Equal(Path.Combine(switchDir, "eden.exe"), exe);
        Assert.True(Directory.Exists(Path.Combine(switchDir, "user")));
        Assert.Equal("meine-keys", File.ReadAllText(keys));
        Assert.Equal("0.2.1", File.ReadAllText(Path.Combine(switchDir, "hub-version.txt")));
        Assert.False(Directory.Exists(Path.Combine(hub.Paths.Integrations, "_staging")) &&
                     Directory.EnumerateFileSystemEntries(Path.Combine(hub.Paths.Integrations, "_staging")).Any());
    }

    [Fact]
    public async Task Local_packages_flag_wrong_archives_in_component_folder()
    {
        using var hub = new TempHub();
        var switchDir = hub.Paths.IntegrationDir("switch");
        Directory.CreateDirectory(switchDir);
        var wrong = MakeZip(hub, "Website-main.zip", "Website-main/README.md");
        File.Copy(wrong, Path.Combine(switchDir, "Website-main.zip"));
        var right = MakeZip(hub, "Eden-Windows-v0.2.1-amd64-clang-pgo.zip", "eden.exe");
        File.Copy(right, Path.Combine(switchDir, "Eden-Windows-v0.2.1-amd64-clang-pgo.zip"));

        var found = await new ComponentInstaller(hub.Paths, hub.Backups).FindLocalPackagesAsync(ComponentIds.Switch);
        Assert.Contains(found, p => p.Path.EndsWith("Website-main.zip") && !p.Valid);
        Assert.Contains(found, p => p.Path.EndsWith("Eden-Windows-v0.2.1-amd64-clang-pgo.zip") && p.Valid);
    }
}
