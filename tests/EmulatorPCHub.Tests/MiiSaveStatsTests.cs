using System.Buffers.Binary;
using System.Text;
using EmulatorPCHub.Core.Mii;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Cemu;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Library;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.Tests;

public class MiiSaveStatsTests
{
    // Standard-Mii, das Cemu für ein neues Konto erzeugt (echte account.dat, CRC von Cemu berechnet)
    private const string CemuDefaultMii =
        "010001100000d73e030034330100010001000100010001000100640065006600610075006c0074000000000000000100010001000100010001000106010001000100010001000100010001000100010001000100010001000100010001000100";

    private static byte[] WiiMii(string name, uint id, int color = 4, bool girl = false)
    {
        var d = new byte[MiiCodec.WiiSize];
        ushort flags = (ushort)((girl ? 1 << 14 : 0) | 3 << 10 | 15 << 5 | color << 1);
        BinaryPrimitives.WriteUInt16BigEndian(d, flags);
        for (var i = 0; i < name.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(2 + i * 2), name[i]);
        BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(0x18), id);
        for (var i = 0; i < "Robin".Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(0x36 + i * 2), "Robin"[i]);
        return d;
    }

    [Fact]
    public void Ver3_crc_matches_cemu_and_rename_keeps_it_valid()
    {
        var data = Convert.FromHexString(CemuDefaultMii);
        Assert.True(MiiCodec.HasValidVer3Crc(data));
        var info = MiiCodec.Read(data)!;
        Assert.Equal(MiiFormat.Ver3, info.Format);
        Assert.Equal("default", info.Name);

        var renamed = MiiCodec.WithName(data, "Robin");
        Assert.Equal("Robin", MiiCodec.Read(renamed)!.Name);
        Assert.True(MiiCodec.HasValidVer3Crc(renamed));
    }

    [Fact]
    public void Wii_mii_fields_are_read()
    {
        var info = MiiCodec.Read(WiiMii("Mario", 0x80001234, color: 0, girl: true))!;
        Assert.Equal(("Mario", "Robin", true, 0, 3, 15), (info.Name, info.Creator, info.IsGirl, info.FavoriteColor, info.BirthMonth, info.BirthDay));
        Assert.Null(MiiCodec.Read(new byte[MiiCodec.WiiSize]));
    }

    [Fact]
    public void Wii_database_put_replaces_same_id_and_fixes_crc()
    {
        using var hub = new TempHub();
        var file = Path.Combine(hub.Root, "RFL_DB.dat");
        var raw = new byte[0x1F1E0];
        "RNOD"u8.CopyTo(raw);
        File.WriteAllBytes(file, raw);

        var db = WiiMiiDatabase.Load(file)!;
        Assert.Equal(0, db.Put(WiiMii("Mario", 1)));
        Assert.Equal(1, db.Put(WiiMii("Luigi", 2)));
        Assert.Equal(0, db.Put(WiiMii("Mario2", 1))); // gleiche Mii-ID → ersetzt
        db.Save();

        var reloaded = WiiMiiDatabase.Load(file)!;
        Assert.True(reloaded.HasValidCrc);
        Assert.Equal(["Mario2", "Luigi"], reloaded.Miis().Select(m => MiiCodec.Read(m.Data)!.Name));
    }

    [Fact]
    public void Cemu_account_mii_is_replaced_and_other_lines_kept()
    {
        using var hub = new TempHub();
        var file = hub.File("mlc01/usr/save/system/act/80000001/account.dat",
            Encoding.UTF8.GetBytes($"AccountInstance_20120705\nPersistentId=80000001\nMiiData={CemuDefaultMii}\nMiiName=00640065006600610075006c00740000000000000000\nGender=0\n"));
        var mii = MiiCodec.WithName(Convert.FromHexString(CemuDefaultMii), "Robin");
        CemuAccounts.SetMii(file, mii, "Robin");

        var acc = CemuAccounts.List(Path.Combine(hub.Root, "mlc01")).Single();
        Assert.Equal("80000001", acc.PersistentId);
        Assert.Equal("Robin", acc.MiiName);
        Assert.Equal(mii, acc.MiiData);
        Assert.Contains("Gender=0", File.ReadAllText(file));
    }

    [Fact]
    public void Eden_profiles_are_listed_renamed_and_mapped_to_save_folders()
    {
        using var hub = new TempHub();
        var data = Path.Combine(hub.Root, "eden");
        var raw = new byte[0x650];
        var uuid = Convert.FromHexString("00112233445566778899aabbccddeeff");
        uuid.CopyTo(raw, 0x10);
        uuid.CopyTo(raw, 0x20);
        "Eden"u8.CopyTo(raw.AsSpan(0x10 + 0x28));
        var file = EdenProfiles.ProfilesFile(data);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, raw);

        var user = EdenProfiles.List(data).Single();
        Assert.Equal(("00112233445566778899aabbccddeeff", "Eden"), (user.Uuid, user.Name));
        EdenProfiles.Rename(data, user.Uuid, "Robin");
        Assert.Equal("Robin", EdenProfiles.List(data).Single().Name);
        // u128: obere 64 Bit (Bytes 8–15, LE) zuerst
        Assert.Equal("FFEEDDCCBBAA99887766554433221100", EdenProfiles.SaveFolderName(user.Uuid));
        Assert.EndsWith(Path.Combine("0000000000000000", "FFEEDDCCBBAA99887766554433221100", "0100152000022000"),
            EdenProfiles.SaveDirectory(data, user.Uuid, "0100152000022000"));
    }

    [Fact]
    public void Save_manager_snapshots_restore_duplicate_and_import_per_profile()
    {
        using var hub = new TempHub();
        hub.File("integrations/cemu/Cemu_2.6/Cemu.exe");
        Directory.CreateDirectory(Path.Combine(hub.Root, "integrations/cemu/Cemu_2.6/portable"));
        var adapters = new AdapterRegistry(hub.Paths, hub.Config, hub.Backups);
        var saves = new SaveManager(hub.Paths, adapters);
        var game = new GameEntry { Id = "wiiu:0005000010116000", Title = "Skylanders Giants", Platform = HubPlatform.WiiU, GameCode = "0005000010116000" };
        var robin = new UserProfile { Name = "Robin", CemuAccount = "80000001" };
        var gast = new UserProfile { Name = "Gast", CemuAccount = "80000002" };

        var loc = saves.Locations(game, robin).First(l => l.Key == "user");
        Assert.EndsWith(Path.Combine("usr", "save", "00050000", "10116000", "user", "80000001"), loc.Path);
        Assert.Null(saves.CreateSnapshot(game, robin, "leer", auto: false));

        var save = Path.Combine(loc.Path, "save.bin");
        Directory.CreateDirectory(loc.Path);
        File.WriteAllText(save, "Level 1");
        var first = saves.CreateSnapshot(game, robin, "Level 1", auto: true)!;
        Assert.Null(saves.CreateSnapshot(game, robin, "unverändert", auto: true)); // keine Änderung → kein Auto-Snapshot

        File.WriteAllText(save, "Level 5");
        saves.Restore(first, game, robin);
        Assert.Equal("Level 1", File.ReadAllText(save));
        Assert.Contains(saves.Snapshots(game, robin.Id), s => s.Manifest.Label == "Vor Wiederherstellen");

        // Duplizieren für ein anderes Profil → landet in dessen Cemu-Konto
        var copy = saves.Duplicate(first, game, gast);
        saves.Restore(copy, game, gast);
        Assert.Equal("Level 1", File.ReadAllText(Path.Combine(saves.Locations(game, gast).First(l => l.Key == "user").Path, "save.bin")));

        // Export → Import
        var exported = saves.Export(first, hub.Root);
        var imported = saves.Import(exported, game, gast);
        Assert.Equal(gast.Id, imported.Manifest.ProfileId);
    }

    [Fact]
    public void Statistics_are_computed_per_game_platform_profile_and_preset()
    {
        var today = new DateTime(2026, 9, 25);
        var games = new Dictionary<string, GameEntry>
        {
            ["mkw"] = new() { Id = "mkw", Title = "Mario Kart Wii", Platform = HubPlatform.Wii },
            ["sky"] = new() { Id = "sky", Title = "Skylanders", Platform = HubPlatform.WiiU },
        };
        DateTimeOffset At(int daysAgo, int hour = 18) => new DateTimeOffset(today.AddDays(-daysAgo).AddHours(hour));
        var sessions = new List<PlaySession>
        {
            new("mkw", "Retro Rewind", "Dolphin", At(0), 3600, 0, "p1"),
            new("mkw", "Vanilla", "Dolphin", At(1), 1800, 0, "p2"),
            new("sky", null, "Cemu", At(40), 600, 0, "p1"),
        };
        var launches = new List<LaunchAttempt> { new("mkw", "p1", At(0), true), new("mkw", "p2", At(1), true), new("mkw", "p1", At(2), false) };

        var s = StatisticsService.Build(sessions, launches, games, today);
        Assert.Equal(TimeSpan.FromSeconds(6000), s.Total);
        Assert.Equal(3, s.Sessions);
        Assert.Equal(TimeSpan.FromHours(1), s.Longest);
        Assert.Equal("Mario Kart Wii", s.LongestGame);
        var mkw = s.Games.First(g => g.GameId == "mkw");
        Assert.Equal((2, 3, 1), (mkw.Sessions, mkw.Starts, mkw.FailedStarts));
        Assert.Equal(TimeSpan.FromMinutes(45), mkw.Average);
        Assert.Equal("Nintendo Wii", s.ByPlatform[0].Key);
        Assert.Equal("p1", s.ByProfile[0].Key);
        Assert.Equal("Mario Kart Wii · Retro Rewind", s.ByPreset[0].Key);
        Assert.Equal(60, s.Last30Days[^1].Total.TotalMinutes);
        Assert.Equal(30, s.Last30Days.Count);
        Assert.Equal(12, s.Last12Weeks.Count);
    }

    [Fact]
    public void Favorites_and_sessions_are_kept_per_profile()
    {
        using var hub = new TempHub();
        var lib = new LibraryService(new LibraryDatabase(hub.Paths.LibraryDb), hub.Paths, hub.Config);
        lib.SetActiveProfile("p1");
        var mk = lib.Find(KnownGames.MarioKartWiiId)!;
        lib.ToggleFavorite(mk);
        lib.RecordSession(mk, "Vanilla", "Dolphin", DateTimeOffset.Now, TimeSpan.FromMinutes(10), 0);

        lib.SetActiveProfile("p2");
        Assert.False(lib.Find(KnownGames.MarioKartWiiId)!.IsFavorite);
        lib.SetActiveProfile("p1");
        Assert.True(lib.Find(KnownGames.MarioKartWiiId)!.IsFavorite);
        Assert.Equal("p1", lib.AllSessions().Single().ProfileId);
        Assert.Equal(1, lib.FavoriteCount("p1"));
    }
}
