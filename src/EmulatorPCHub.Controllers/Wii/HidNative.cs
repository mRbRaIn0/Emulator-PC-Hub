using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EmulatorPCHub.Controllers.Wii;

/// <summary>Ein HID-Gerät aus der Windows-Geräteliste (nur Nintendo-/Mayflash-Geräte werden geöffnet).</summary>
public sealed record HidDeviceInfo(string Path, ushort Vendor, ushort Product, ushort UsagePage, ushort Usage,
    int InputReportLength, int OutputReportLength, string? ProductName)
{
    /// <summary>Über den Windows-Bluetooth-Stack verbunden (HID-over-Bluetooth-GUID im Pfad).</summary>
    public bool Bluetooth => Path.Contains("00001124-0000-1000-8000-00805f9b34fb", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Minimale SetupAPI-/HID-Anbindung, um Wii Remotes und die DolphinBar direkt anzusprechen.</summary>
internal static partial class HidNative
{
    private const int DigcfPresent = 0x02;
    private const int DigcfDeviceInterface = 0x10;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x3;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle h, ref HiddAttributes a);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, ref HidpCaps caps);
    [DllImport("hid.dll")] private static extern bool HidD_GetProductString(SafeFileHandle h, byte[] buffer, int length);
    [DllImport("hid.dll")] public static extern bool HidD_SetOutputReport(SafeFileHandle h, byte[] buffer, int length);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, int index, ref DeviceInterfaceData data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData data, IntPtr detail, int size,
        out int required, IntPtr info);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);

    /// <summary>Alle HID-Pfade, deren Pfad zu einem der Filter passt.</summary>
    public static List<HidDeviceInfo> Enumerate(Func<string, bool> pathFilter)
    {
        var result = new List<HidDeviceInfo>();
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (set == IntPtr.Zero || set == new IntPtr(-1))
            return result;
        try
        {
            for (var i = 0; ; i++)
            {
                var data = new DeviceInterfaceData { Size = Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data))
                    break;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                if (size <= 0)
                    continue;
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize = 8 (x64), danach der Pfad
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, size, out _, IntPtr.Zero))
                        continue;
                    var path = Marshal.PtrToStringUni(buffer + 4);
                    if (string.IsNullOrEmpty(path) || !pathFilter(path))
                        continue;
                    if (Describe(path) is { } info)
                        result.Add(info);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return result;
    }

    private static HidDeviceInfo? Describe(string path)
    {
        using var h = CreateFile(path, 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (h.IsInvalid)
            return null;
        var attr = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(h, ref attr))
            return null;
        var caps = new HidpCaps { Reserved = new ushort[17] };
        if (HidD_GetPreparsedData(h, out var pre))
        {
            HidP_GetCaps(pre, ref caps);
            HidD_FreePreparsedData(pre);
        }
        string? product = null;
        var name = new byte[256];
        if (HidD_GetProductString(h, name, name.Length))
            product = System.Text.Encoding.Unicode.GetString(name).TrimEnd('\0').Trim();
        return new HidDeviceInfo(path, attr.VendorId, attr.ProductId, caps.UsagePage, caps.Usage,
            caps.InputReportByteLength, caps.OutputReportByteLength, string.IsNullOrEmpty(product) ? null : product);
    }

    /// <summary>Gerät zum Lesen/Schreiben öffnen (überlappend, geteilt mit Emulatoren/SDL).</summary>
    public static SafeFileHandle OpenReadWrite(string path) =>
        CreateFile(path, GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
}
