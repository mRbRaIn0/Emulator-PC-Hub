using System.Runtime.InteropServices;

namespace EmulatorPCHub.Emulation;

/// <summary>Liest die Prozessliste (inkl. Eltern-PID) über die Toolhelp-API.</summary>
public static class ProcessTree
{
    public sealed record Entry(int Pid, int ParentPid, string ExeName);

    public static List<Entry> Snapshot()
    {
        var list = new List<Entry>();
        if (!OperatingSystem.IsWindows())
            return list;
        var snap = CreateToolhelp32Snapshot(0x2, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1))
            return list;
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snap, ref entry))
                return list;
            do
            {
                list.Add(new Entry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile));
            } while (Process32NextW(snap, ref entry));
        }
        finally
        {
            CloseHandle(snap);
        }
        return list;
    }

    /// <summary>Alle Nachkommen eines Prozesses.</summary>
    public static HashSet<int> Descendants(int rootPid, List<Entry>? snapshot = null)
    {
        snapshot ??= Snapshot();
        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            foreach (var e in snapshot)
            {
                if (e.ParentPid == pid && e.Pid != rootPid && result.Add(e.Pid))
                    queue.Enqueue(e.Pid);
            }
        }
        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
