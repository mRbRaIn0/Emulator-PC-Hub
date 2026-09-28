using System.Buffers.Binary;
using System.Text;
using EmulatorPCHub.Controllers.Wii;
using EmulatorPCHub.Emulation.Dolphin;

namespace EmulatorPCHub.Tests;

public class WiiRemoteTests
{
    private static HidDeviceInfo Hid(string path, ushort vid, ushort pid, ushort usage = 0x05, string? name = null) =>
        new(path, vid, pid, 0x01, usage, 22, 22, name);

    [Fact]
    public void DolphinBar_Mode3_IsDetectedFromMayflashGamepadCollections()
    {
        var devices = new[]
        {
            Hid(@"\\?\hid#vid_0079&pid_1803&col01#7&1939a80b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x0079, 0x1803, name: "Mayflash Wiimote PC Adapter"),
            Hid(@"\\?\hid#vid_0079&pid_1803&col02#7&1939a80b&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x0079, 0x1803),
        };
        Assert.Equal(DolphinBarMode.Gamepad, WiiRemoteProtocol.DetectDolphinBar(devices));
    }

    [Fact]
    public void DolphinBar_Mode4_IsUsbWiiRemote_AndBluetoothRemoteIsNot()
    {
        var usb = Hid(@"\\?\hid#vid_057e&pid_0306&col02#8&abc&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x057E, 0x0306);
        var bt = Hid(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0002057e_pid&0306#9&1&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", 0x057E, 0x0306);
        Assert.Equal(DolphinBarMode.Dolphin, WiiRemoteProtocol.DetectDolphinBar([usb]));
        Assert.Equal(DolphinBarMode.None, WiiRemoteProtocol.DetectDolphinBar([bt]));
        Assert.True(bt.Bluetooth);
        Assert.False(usb.Bluetooth);
        Assert.Equal(2, WiiRemoteProtocol.SlotFromPath(usb.Path));
        Assert.Equal(4, WiiRemoteProtocol.SlotFromPath(@"\?\hid#vid_057e&pid_0306&mi_03#8&e2ac850&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"));
        Assert.True(WiiRemoteProtocol.IsCandidatePath(bt.Path));
        Assert.False(WiiRemoteProtocol.IsCandidatePath(@"\\?\hid#vid_046d&pid_c52b#x"));
    }

    [Fact]
    public void StatusReport_ParsesBatteryLedsAndExtension()
    {
        // 0x20, Tasten, Flags (LED 2 + Erweiterung), 2× reserviert, Batterie 0x64 (= 50 %)
        var s = WiiRemoteProtocol.ParseStatus(new byte[] { 0x20, 0x00, 0x00, 0x22, 0x00, 0x00, 0x64 })!;
        Assert.Equal(2, s.Leds);
        Assert.True(s.ExtensionConnected);
        Assert.False(s.BatteryLow);
        Assert.Equal(50, s.BatteryPercent);
        Assert.Equal(2, WiiRemoteProtocol.PlayerFromLeds(s.Leds));
        Assert.Equal(8, WiiRemoteProtocol.LedsForPlayer(4));
        Assert.Equal(new byte[] { 0x11, 0x40 }, WiiRemoteProtocol.SetLeds(WiiRemoteProtocol.LedsForPlayer(3)));
    }

    [Fact]
    public void ExtendedIr_ParsesDotsAndSkipsEmptySlots()
    {
        // Punkt 1: X = 0x2_10 = 528, Y = 0x1_80 = 384, Größe 3 → Byte 3 = (Y-hi 01 << 6) | (X-hi 10 << 4) | 3 = 0x63
        var ir = new byte[] { 0x10, 0x80, 0x63, 0xC8, 0x80, 0x52, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var dots = WiiRemoteProtocol.ParseExtendedIr(ir);
        Assert.Equal(2, dots.Count);
        Assert.Equal(new IrDot(528, 384, 3), dots[0]);
        Assert.Equal(new IrDot(456, 384, 2), dots[1]);
        var pointer = WiiRemoteProtocol.Pointer(dots)!.Value;
        Assert.InRange(pointer.X, 0.5, 0.52);
        Assert.InRange(pointer.Y, 0.49, 0.51);
        Assert.Null(WiiRemoteProtocol.Pointer([dots[0]]));
    }

    [Fact]
    public void Buttons_AndExtensionIds()
    {
        Assert.Equal(["↑", "A", "Home"], WiiRemoteProtocol.Buttons(0x08, 0x88));
        Assert.Equal(WiiExtension.Nunchuk, WiiRemoteProtocol.IdentifyExtension(new byte[] { 0, 0, 0xA4, 0x20, 0, 0 }));
        Assert.Equal(WiiExtension.ClassicController, WiiRemoteProtocol.IdentifyExtension(new byte[] { 0, 0, 0xA4, 0x20, 1, 1 }));
        Assert.Equal(WiiExtension.Unknown, WiiRemoteProtocol.IdentifyExtension(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));
        var write = WiiRemoteProtocol.WriteRegister(0xB00030, 0x08);
        Assert.Equal(new byte[] { 0x16, 0x04, 0xB0, 0x00, 0x30, 0x01, 0x08 }, write[..7]);
    }

    /// <summary>Minimales SYSCONF wie auf der Wii: Kopf, Offset-Tabelle, Einträge, „SCed“ am Ende.</summary>
    private static byte[] Sysconf()
    {
        var data = new byte[0x4000];
        "SCv0"u8.CopyTo(data);
        var entries = new (string Name, int Type, byte[] Value)[]
        {
            ("BT.SENS", 5, [0, 0, 0, 3]),
            ("BT.BAR", 3, [1]),
            ("BT.SPKV", 3, [0x58]),
            ("BT.MOT", 3, [1]),
        };
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), (ushort)entries.Length);
        var pos = 6 + 2 * (entries.Length + 1);
        for (var i = 0; i < entries.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6 + 2 * i), (ushort)pos);
            var (name, type, value) = entries[i];
            data[pos++] = (byte)((type << 5) | (name.Length - 1));
            Encoding.ASCII.GetBytes(name).CopyTo(data, pos);
            pos += name.Length;
            value.CopyTo(data, pos);
            pos += value.Length;
        }
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6 + 2 * entries.Length), (ushort)pos);
        "SCed"u8.CopyTo(data.AsSpan(data.Length - 4));
        return data;
    }

    [Fact]
    public void Sysconf_ReadsAndWritesSensorBarSettingsInPlace()
    {
        var original = Sysconf();
        var sys = WiiSysconf.FromBytes((byte[])original.Clone())!;
        Assert.Equal(1, sys.Get(WiiSysconf.SensorBarPosition));
        Assert.Equal(3, sys.Get(WiiSysconf.SensorBarSensitivity));
        Assert.Equal(0x58, sys.Get(WiiSysconf.SpeakerVolume));

        Assert.True(sys.Set(WiiSysconf.SensorBarPosition, 0));
        Assert.True(sys.Set(WiiSysconf.SensorBarSensitivity, 5));
        Assert.True(sys.Set(WiiSysconf.WiimoteMotor, 0));
        Assert.False(sys.Set("BT.XYZ", 1));

        Assert.Equal(0, sys.Get(WiiSysconf.SensorBarPosition));
        Assert.Equal(5, sys.Get(WiiSysconf.SensorBarSensitivity));
        Assert.Equal(0, sys.Get(WiiSysconf.WiimoteMotor));
        // Nur die drei Wertbytes haben sich geändert
        Assert.Equal(3, original.Zip(sys.Bytes).Count(p => p.First != p.Second));
        Assert.Null(WiiSysconf.FromBytes(new byte[32]));
    }

    [Fact]
    public void Sysconf_ReadsRealDolphinFileIfPresent()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "EmulatorPCHub.root")))
            root = Path.GetDirectoryName(root);
        if (root == null)
            return;
        var path = Path.Combine(root, "integrations", "dolphin", "Dolphin-x64", "User", "Wii", "shared2", "sys", "SYSCONF");
        if (!File.Exists(path))
            return;
        var sys = WiiSysconf.Load(path)!;
        Assert.InRange(sys.Get(WiiSysconf.SensorBarPosition)!.Value, 0, 1);
        Assert.InRange(sys.Get(WiiSysconf.SensorBarSensitivity)!.Value, 1, 5);
    }
}
