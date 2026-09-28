using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Tests;

public class LibraryTests
{
    [Fact]
    public void DiscHeader_Iso_Wii_is_detected()
    {
        using var hub = new TempHub();
        var file = hub.File("Wii/mkwii.iso", Fake.DiscHeader("RMCP01", "MARIO KART Wii", wii: true));
        var info = DiscHeaderReader.Read(file);
        Assert.NotNull(info);
        Assert.Equal(HubPlatform.Wii, info.Platform);
        Assert.Equal("RMCP01", info.GameCode);
    }

    [Fact]
    public void DiscHeader_Rvz_GameCube_is_detected()
    {
        using var hub = new TempHub();
        var file = hub.File("GC/dd.rvz", Fake.Rvz("GM4P01", "Mario Kart Double Dash", wii: false));
        var info = DiscHeaderReader.Read(file);
        Assert.NotNull(info);
        Assert.Equal(HubPlatform.GameCube, info.Platform);
        Assert.Equal("GM4P01", info.GameCode);
    }

    [Theory]
    [InlineData("0100152000022000", SwitchContentKind.Base, "0100152000022000")]
    [InlineData("0100152000022800", SwitchContentKind.Update, "0100152000022000")]
    [InlineData("0100152000023001", SwitchContentKind.Dlc, "0100152000022000")]
    [InlineData("0100152000023006", SwitchContentKind.Dlc, "0100152000022000")]
    public void Switch_title_ids_are_classified(string tid, SwitchContentKind kind, string baseTid)
    {
        Assert.Equal(kind, SwitchFileReader.Classify(tid));
        Assert.Equal(baseTid, SwitchFileReader.BaseTitleId(tid));
    }

    [Fact]
    public void Nsp_ticket_gives_title_id_and_ncas()
    {
        using var hub = new TempHub();
        var file = hub.File("Switch/dlc.nsp", Fake.Nsp(
            "0100152000023001000000000000000a.tik", "0100152000023001000000000000000a.cert",
            "abcdef0123456789abcdef0123456789.nca", "11112222333344445555666677778888.cnmt.nca"));
        var info = SwitchFileReader.Read(file);
        Assert.Equal("0100152000023001", info.TitleId);
        Assert.Equal(SwitchContentKind.Dlc, info.SwitchKind);
        Assert.Equal(2, info.NcaFiles.Count);
    }

    [Fact]
    public void Scanner_finds_games_updates_dlc_and_dedupes_formats()
    {
        using var hub = new TempHub();
        hub.File("Wii/Mario Kart Wii.iso", Fake.DiscHeader("RMCP01", "MARIO KART Wii", wii: true));
        hub.File("Wii/Mario Kart Wii.rvz", Fake.Rvz("RMCP01", "MARIO KART Wii", wii: true));
        hub.File("Switch/Mario Kart 8 Deluxe [0100152000022000][v0].nsp", Fake.Nsp("x.nca"));
        hub.File("Switch/Mario Kart 8 Deluxe [0100152000022800][v3.0.3].nsp", Fake.Nsp("y.nca"));
        hub.File("Switch/MK8D DLC [0100152000023001].nsp", Fake.Nsp("0100152000023001000000000000000a.tik", "z.nca"));
        hub.File("WiiU/Game (Europe).wud", new byte[16]);
        hub.File("WiiU/Game (Europe).wux", new byte[16]);

        var result = new GameScanner().Scan([new ScanRoot(hub.Root, null)]);

        var mkwii = Assert.Single(result.Games, g => g.Special == SpecialPage.MarioKartWii);
        Assert.EndsWith(".rvz", mkwii.Path); // RVZ wird bevorzugt
        Assert.Single(result.Games, g => g.Special == SpecialPage.MarioKart8Deluxe);
        Assert.Single(result.Games, g => g.Platform == HubPlatform.WiiU);
        var addons = result.SwitchAddOns[KnownGames.MarioKart8DeluxeTitleId];
        Assert.Single(addons.Updates);
        Assert.Single(addons.Dlcs);
    }

    [Fact]
    public void Database_keeps_favorites_and_playtime()
    {
        using var hub = new TempHub();
        var db = new LibraryDatabase(hub.Paths.LibraryDb);
        var game = new GameEntry { Id = "wii:TEST01", Title = "Test", Platform = HubPlatform.Wii, EmulatorId = EmulatorIds.Dolphin };
        db.Upsert(game);
        game.IsFavorite = true;
        db.Upsert(game);
        db.AddSession(new PlaySession(game.Id, "Vanilla", "Dolphin", DateTimeOffset.Now, 125, 0));
        var loaded = Assert.Single(db.LoadGames(), g => g.Id == game.Id);
        Assert.True(loaded.IsFavorite);
        Assert.Equal(125, loaded.PlayTimeSeconds);
        Assert.NotNull(loaded.LastPlayed);
    }

    [Fact]
    public void LibraryService_always_has_mario_kart_entries()
    {
        using var hub = new TempHub();
        var lib = new LibraryService(new LibraryDatabase(hub.Paths.LibraryDb), hub.Paths, hub.Config);
        Assert.NotNull(lib.Find(KnownGames.MarioKartWiiId));
        Assert.NotNull(lib.Find(KnownGames.MarioKart8DeluxeId));
        Assert.True(lib.Find(KnownGames.MarioKartWiiId)!.IsPlaceholder);
    }

    [Fact]
    public void Custom_name_cover_and_hidden_flag_are_persisted()
    {
        using var hub = new TempHub();
        var lib = new LibraryService(new LibraryDatabase(hub.Paths.LibraryDb), hub.Paths, hub.Config);
        var mk = lib.Find(KnownGames.MarioKartWiiId)!;
        mk.Title = "Mein Mario Kart";
        mk.CustomTitle = true;
        mk.HiddenOnHome = true;
        lib.Save(mk);
        var image = hub.File("bilder/cover.PNG", [1, 2, 3]);
        lib.SetCover(mk, image);

        var reloaded = new LibraryService(new LibraryDatabase(hub.Paths.LibraryDb), hub.Paths, hub.Config);
        var g = reloaded.Find(KnownGames.MarioKartWiiId)!;
        Assert.Equal("Mein Mario Kart", g.Title);
        Assert.True(g.CustomTitle && g.HiddenOnHome);
        Assert.DoesNotContain(reloaded.HomeOrder(), x => x.Id == g.Id);
        Assert.Equal(Path.Combine(reloaded.ArtworkDir(g), "cover.png"), g.CoverPath);

        reloaded.ResetCover(g);
        Assert.Null(g.CoverPath);
    }

    [Fact]
    public void Cover_source_is_kept_for_recropping_and_removed_on_reset()
    {
        using var hub = new TempHub();
        var lib = new LibraryService(new LibraryDatabase(hub.Paths.LibraryDb), hub.Paths, hub.Config);
        var mk = lib.Find(KnownGames.MarioKartWiiId)!;
        var original = hub.File("bilder/hochkant.JPG", [1, 2, 3]);
        var cropped = hub.File("tmp/quadrat.png", [4, 5, 6]);

        lib.SetCoverSource(mk, original);
        lib.SetCover(mk, cropped);
        var source = Path.Combine(lib.ArtworkDir(mk), "cover-source.jpg");
        Assert.Equal(source, lib.CoverSourcePath(mk));
        Assert.Equal(Path.Combine(lib.ArtworkDir(mk), "cover.png"), mk.CoverPath);

        // Neu zuschneiden vom gemerkten Original: Quelle bleibt unverändert erhalten
        lib.SetCoverSource(mk, source);
        lib.SetCover(mk, cropped);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(source));

        lib.ResetCover(mk);
        Assert.Null(lib.CoverSourcePath(mk));
    }

    [Fact]
    public void Clean_file_names()
    {
        Assert.Equal("Skylanders Giants", TitleDatabase.CleanFileName(@"E:\x\Skylanders Giants (Europe) (En,Fr).wux"));
        Assert.Equal("Mario Kart 8 Deluxe", TitleDatabase.CleanFileName(@"Mario Kart 8 Deluxe [0100152000022000][v0].nsp"));
    }
}
