using System.Buffers.Binary;
using EmulatorPCHub.Core.Mii;
using Xunit;

namespace EmulatorPCHub.Tests;

public class MiiConvertTests
{
    // Wii-Mii aus dem Mii-Kanal (Dolphin RFL_DB.dat, System-ID genullt): „Robin“, männlich, Standardpositionen
    private static byte[] Robin() => Convert.FromHexString(
        "18a40052006f00620069006e000000000000000000007c1889c048510000000000045781097d0692808c08481449a88d0a8a008a2505" +
        "0000000000000000000000000000000000000000");

    [Fact]
    public void Wii_mii_converts_to_valid_ver3_with_same_features()
    {
        var v = MiiConvert.WiiToVer3(Robin());
        Assert.True(MiiCodec.HasValidVer3Crc(v));
        var info = MiiCodec.Read(v)!;
        Assert.Equal("Robin", info.Name);
        Assert.False(info.IsGirl);
        Assert.Equal(6, info.BirthMonth);
        Assert.Equal(5, info.BirthDay);
        Assert.Equal(43, v[0x32]); // Frisur
        var eye = BinaryPrimitives.ReadUInt32LittleEndian(v.AsSpan(0x34));
        Assert.Equal(32u, eye & 0x3F);        // Augentyp
        Assert.Equal(12u, eye >> 25 & 0x1F);  // Augenhöhe
        Assert.Equal(3u, eye >> 13 & 0x7);    // Streckung (Standard)
    }

    [Fact]
    public void Switch_store_data_matches_eden_layout_and_checksum()
    {
        var s = MiiConvert.ToSwitchStoreData(Robin(), Guid.Parse("00112233-4455-4677-8899-aabbccddeeff"));
        Assert.Equal(0x44, s.Length);
        Assert.Equal("Robin", MiiConvert.SwitchName(s));
        Assert.Equal(MiiCodec.Crc16(s.AsSpan(0, 0x40)), BinaryPrimitives.ReadUInt16BigEndian(s.AsSpan(0x40)));
        var w0 = BinaryPrimitives.ReadUInt32LittleEndian(s);
        Assert.Equal(43u, w0 & 0xFF);          // Frisur
        Assert.Equal(124u, w0 >> 8 & 0x7F);    // Größe
        Assert.Equal(6u, w0 >> 24 & 0x7F);     // Haarfarbe 6 bleibt 6
        var w1 = BinaryPrimitives.ReadUInt32LittleEndian(s.AsSpan(4));
        Assert.Equal(8u, w1 & 0x7F);           // Augenfarbe 0 → Switch 8
        Assert.Equal(8u, w1 >> 8 & 0x7F);      // Augenbrauenfarbe 0 → Switch 8
        var w6 = BinaryPrimitives.ReadUInt32LittleEndian(s.AsSpan(24));
        Assert.Equal(9u - 3, w6 >> 12 & 0xF);  // Augenbrauenhöhe: Switch = Wii − 3
    }

    [Fact]
    public void Edited_wii_mii_updates_its_switch_slot_instead_of_duplicating()
    {
        var file = Path.Combine(Path.GetTempPath(), $"nfdb-{Guid.NewGuid():N}.dat");
        try
        {
            var db = SwitchMiiDatabase.LoadOrCreate(file);
            var wii = Robin();
            db.Upsert(MiiConvert.ToSwitchStoreData(wii, MiiConvert.StableCreateId(wii)), out var added);
            Assert.True(added);
            db.Upsert(MiiConvert.ToSwitchStoreData(wii, MiiConvert.StableCreateId(wii)), out var again);
            Assert.False(again);

            var edited = MiiCodec.WithName(wii, "Robert"); // im Mii-Kanal umbenannt, gleiche Mii-ID
            Assert.Equal(0, db.Upsert(MiiConvert.ToSwitchStoreData(edited, MiiConvert.StableCreateId(edited)), out var updated));
            Assert.True(updated);
            Assert.Equal(["Robert"], db.Names);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Switch_database_crc_matches_eden_and_add_is_idempotent()
    {
        var file = Path.Combine(Path.GetTempPath(), $"nfdb-{Guid.NewGuid():N}.dat");
        try
        {
            var db = SwitchMiiDatabase.LoadOrCreate(file);
            db.Save();
            var empty = File.ReadAllBytes(file);
            Assert.Equal(SwitchMiiDatabase.FileSize, empty.Length);
            Assert.Equal(0x60B7, BinaryPrimitives.ReadUInt16BigEndian(empty.AsSpan(0x1A96))); // von Eden erzeugte leere Datenbank

            var mii = MiiConvert.ToSwitchStoreData(Robin());
            Assert.Equal(0, db.Add(mii));
            Assert.Equal(0, db.Add(MiiConvert.ToSwitchStoreData(Robin()))); // gleiches Mii → kein Duplikat
            db.Save();
            var reloaded = SwitchMiiDatabase.LoadOrCreate(file);
            Assert.Equal(1, reloaded.Count);
            Assert.Equal(["Robin"], reloaded.Names);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
