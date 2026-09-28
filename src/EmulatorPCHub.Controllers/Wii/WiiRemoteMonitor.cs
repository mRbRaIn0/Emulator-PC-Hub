using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Controllers.Wii;

/// <summary>Momentaufnahme einer Wii Remote (über DolphinBar Mode 4 oder Windows-Bluetooth).</summary>
public sealed record WiiRemoteInfo(string Path, int Slot, bool ViaDolphinBar, bool IsPlus, bool Responding,
    WiiRemoteStatus? Status, WiiExtension Extension)
{
    public string Label => ViaDolphinBar ? $"DolphinBar-Slot {(Slot > 0 ? Slot : "?")}" : "Bluetooth";
}

/// <summary>Ein Messwert des Sensortests.</summary>
public sealed record WiiSensorSample(IReadOnlyList<IrDot> Dots, IReadOnlyList<string> Buttons, (int X, int Y, int Z) Accel, DateTime At);

/// <summary>
/// Spricht Wii Remotes direkt über HID an (DolphinBar Mode 4 oder Bluetooth): Verbindungen, Akku, Erweiterung, LEDs
/// und ein IR-Sensortest (liest die Punkte der Sensorleiste über die Kamera der Wii Remote).
/// Läuft nur, solange die Controller-Seite offen ist, und gibt alle Geräte vor einem Spielstart frei.
/// Alle Zugriffe laufen auf einem eigenen Thread; die Oberfläche liest nur Momentaufnahmen.
/// </summary>
public sealed class WiiRemoteMonitor : IDisposable
{
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentDictionary<string, int> _desiredLeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Connection> _connections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, WiiExtension> _extensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly AutoResetEvent _wake = new(false);
    private Thread? _thread;
    private volatile bool _stop;
    private volatile string? _sensorPath;
    private string? _sensorActivePath;
    private DateTime _nextScan;

    public DolphinBarMode BarMode { get; private set; }
    public IReadOnlyList<WiiRemoteInfo> Remotes { get; private set; } = [];
    public bool HasScanned { get; private set; }
    public bool Running => _thread != null;
    /// <summary>Hub setzt die LEDs selbst (sonst übernimmt das SDL, weil es die Wii Remotes als Controller führt).</summary>
    public bool HubControlsLeds { get; set; } = true;

    public string? SensorPath => _sensorPath;
    public WiiSensorSample? LatestSample { get; private set; }
    public string SensorState { get; private set; } = "";

    /// <summary>Neue Momentaufnahme verfügbar (Hintergrund-Thread).</summary>
    public event Action? Changed;

    public void Start()
    {
        if (_thread != null)
            return;
        _stop = false;
        _nextScan = DateTime.MinValue;
        _thread = new Thread(Loop) { IsBackground = true, Name = "WiiRemoteMonitor" };
        _thread.Start();
    }

    /// <summary>Beenden und alle Geräte freigeben (z. B. vor dem Spielstart, damit Dolphin sie exklusiv hat).</summary>
    public void Stop()
    {
        var t = _thread;
        if (t == null)
            return;
        _stop = true;
        _wake.Set();
        t.Join(TimeSpan.FromSeconds(3));
        _thread = null;
    }

    public void Dispose() => Stop();

    public void RefreshNow()
    {
        _nextScan = DateTime.MinValue;
        _wake.Set();
    }

    /// <summary>Gewünschte LEDs einer Wii Remote (Spieler 1–4 → LED 1–4); 0 = nicht verändern.</summary>
    public void SetPlayer(string path, int player)
    {
        var mask = WiiRemoteProtocol.LedsForPlayer(player);
        if (mask == 0)
            _desiredLeds.TryRemove(path, out _);
        else
            _desiredLeds[path] = mask;
    }

    /// <summary>Kurz vibrieren und alle LEDs aufleuchten lassen.</summary>
    public void Identify(string path)
    {
        _commands.Enqueue(() =>
        {
            var c = Get(path);
            if (c == null)
                return;
            c.Write(WiiRemoteProtocol.SetLeds(0xF, rumble: true));
            Thread.Sleep(350);
            c.Write(WiiRemoteProtocol.SetLeds(_desiredLeds.TryGetValue(path, out var m) ? m : 0xF));
        });
        _wake.Set();
    }

    public void StartSensorTest(string path)
    {
        _sensorPath = path;
        LatestSample = null;
        SensorState = "Kamera wird eingeschaltet …";
        _wake.Set();
    }

    public void StopSensorTest()
    {
        _sensorPath = null;
        _wake.Set();
    }

    // ------------------------------------------------------------------

    private void Loop()
    {
        try
        {
            while (!_stop)
            {
                while (_commands.TryDequeue(out var cmd))
                    Try(cmd);

                if (_sensorPath != _sensorActivePath)
                {
                    if (_sensorActivePath != null)
                        Try(() => EndSensor(_sensorActivePath));
                    _sensorActivePath = _sensorPath;
                    if (_sensorActivePath != null)
                        Try(() => BeginSensor(_sensorActivePath));
                }

                if (_sensorActivePath != null)
                {
                    Try(() => SensorStep(_sensorActivePath));
                    continue;
                }

                if (DateTime.UtcNow >= _nextScan)
                {
                    Try(Scan);
                    _nextScan = DateTime.UtcNow.AddSeconds(2);
                    Changed?.Invoke();
                }
                _wake.WaitOne(100);
            }
            if (_sensorActivePath != null)
                Try(() => EndSensor(_sensorActivePath));
        }
        finally
        {
            _sensorActivePath = null;
            _sensorPath = null;
            foreach (var c in _connections.Values)
                c.Dispose();
            _connections.Clear();
        }
    }

    private static void Try(Action a)
    {
        try { a(); }
        catch (Exception ex) { HubLog.Warn("Wii Remote: " + ex.Message); }
    }

    private void Scan()
    {
        var devices = HidNative.Enumerate(WiiRemoteProtocol.IsCandidatePath);
        BarMode = WiiRemoteProtocol.DetectDolphinBar(devices);
        var wii = devices.Where(d => WiiRemoteProtocol.IsWiiRemote(d.Vendor, d.Product))
            .OrderBy(d => d.Bluetooth).ThenBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var gone in _connections.Keys.Where(k => wii.All(d => !d.Path.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            _connections[gone].Dispose();
            _connections.Remove(gone);
            _extensions.Remove(gone);
        }

        var list = new List<WiiRemoteInfo>();
        var barIndex = 0;
        foreach (var d in wii)
        {
            var slot = 0;
            if (!d.Bluetooth)
            {
                slot = WiiRemoteProtocol.SlotFromPath(d.Path);
                if (slot == 0)
                    slot = ++barIndex;
            }
            var c = Get(d.Path, d.OutputReportLength, d.InputReportLength);
            WiiRemoteStatus? status = null;
            if (c != null && c.Write(WiiRemoteProtocol.RequestStatus()))
            {
                var r = c.ReadUntil(0x20, 350);
                if (r != null)
                    status = WiiRemoteProtocol.ParseStatus(r);
            }
            var ext = WiiExtension.None;
            if (status != null && c != null)
            {
                if (status.ExtensionConnected)
                {
                    if (!_extensions.TryGetValue(d.Path, out ext) || ext == WiiExtension.None)
                        _extensions[d.Path] = ext = IdentifyExtension(c);
                }
                else
                {
                    _extensions.Remove(d.Path);
                }
                if (HubControlsLeds)
                {
                    if (_desiredLeds.TryGetValue(d.Path, out var mask) && mask != status.Leds)
                    {
                        c.Write(WiiRemoteProtocol.SetLeds(mask));
                        status = status with { Leds = mask };
                    }
                    // Nach einem Status-Report muss der Berichtsmodus neu gesetzt werden, sonst schweigt die Wii Remote.
                    c.Write(WiiRemoteProtocol.SetReportMode(0x30, continuous: false));
                }
            }
            list.Add(new WiiRemoteInfo(d.Path, slot, !d.Bluetooth, d.Product == WiiRemoteProtocol.WiiRemotePlusProduct,
                status != null, status, ext));
        }
        Remotes = list;
        HasScanned = true;
    }

    private static WiiExtension IdentifyExtension(Connection c)
    {
        foreach (var r in WiiRemoteProtocol.ExtensionInitSequence())
        {
            c.Write(r);
            c.ReadUntil(0x22, 120);
        }
        if (!c.Write(WiiRemoteProtocol.ReadRegister(0xA400FA, 6)))
            return WiiExtension.Unknown;
        var resp = c.ReadUntil(0x21, 300);
        if (resp == null || resp.Length < 12 || (resp[3] & 0x0F) != 0)
            return WiiExtension.Unknown;
        return WiiRemoteProtocol.IdentifyExtension(resp.AsSpan(6, 6));
    }

    private int _foreignReports;
    private DateTime _lastSample;

    private void BeginSensor(string path)
    {
        var c = Get(path);
        if (c == null)
        {
            SensorState = "Wii Remote nicht erreichbar";
            return;
        }
        foreach (var r in WiiRemoteProtocol.IrInitSequence())
        {
            if (!c.Write(r))
            {
                SensorState = "Wii Remote antwortet nicht – 1+2 drücken und erneut versuchen";
                return;
            }
            c.ReadUntil(0x22, 80);
            Thread.Sleep(30);
        }
        _foreignReports = 0;
        _lastSample = DateTime.UtcNow;
        SensorState = "Kamera an";
    }

    private void SensorStep(string path)
    {
        var c = Get(path);
        if (c == null)
        {
            SensorState = "Wii Remote getrennt";
            _wake.WaitOne(200);
            return;
        }
        var r = c.Read(120);
        if (r != null && r.Length >= 18 && r[0] == 0x33)
        {
            _foreignReports = 0;
            _lastSample = DateTime.UtcNow;
            LatestSample = new WiiSensorSample(WiiRemoteProtocol.ParseExtendedIr(r.AsSpan(6, 12)),
                WiiRemoteProtocol.Buttons(r[1], r[2]), WiiRemoteProtocol.ParseAccel(r), DateTime.UtcNow);
            SensorState = "Kamera an";
            return;
        }
        // Jemand anderes (z. B. SDL) hat den Berichtsmodus umgestellt oder es kommt nichts → erneut einschalten
        if (r != null && r[0] is >= 0x20 and < 0x40 && r[0] != 0x22)
            _foreignReports++;
        if (_foreignReports > 15 || DateTime.UtcNow - _lastSample > TimeSpan.FromSeconds(1.5))
        {
            _foreignReports = 0;
            _lastSample = DateTime.UtcNow;
            SensorState = "Keine Kameradaten – Kamera wird neu gestartet …";
            BeginSensor(path);
        }
    }

    private void EndSensor(string path)
    {
        var c = Get(path);
        LatestSample = null;
        SensorState = "";
        if (c == null)
            return;
        c.Write(WiiRemoteProtocol.IrCamera(false));
        c.Write(WiiRemoteProtocol.IrCamera2(false));
        c.Write(WiiRemoteProtocol.SetReportMode(0x30, continuous: false));
        // Status anfordern: SDL setzt daraufhin seinen eigenen Berichtsmodus wieder
        c.Write(WiiRemoteProtocol.RequestStatus());
    }

    private Connection? Get(string path, int outLen = 22, int inLen = 22)
    {
        if (_connections.TryGetValue(path, out var c))
            return c;
        var h = HidNative.OpenReadWrite(path);
        if (h.IsInvalid)
        {
            h.Dispose();
            return null;
        }
        c = new Connection(h, outLen > 0 ? outLen : 22, inLen > 0 ? inLen : 22);
        _connections[path] = c;
        return c;
    }

    /// <summary>Geöffnetes HID-Gerät mit Zeitlimits für Lesen/Schreiben.</summary>
    private sealed class Connection : IDisposable
    {
        private readonly SafeFileHandle _handle;
        private readonly FileStream _stream;
        private readonly int _outLen;
        private readonly int _inLen;
        private Task<int>? _pending;
        private byte[] _pendingBuffer = [];

        public Connection(SafeFileHandle handle, int outLen, int inLen)
        {
            _handle = handle;
            _outLen = outLen;
            _inLen = inLen;
            _stream = new FileStream(handle, FileAccess.ReadWrite, 0, isAsync: true);
        }

        public bool Write(byte[] report)
        {
            var buffer = new byte[Math.Max(_outLen, report.Length)];
            report.CopyTo(buffer, 0);
            try
            {
                using var cts = new CancellationTokenSource(250);
                _stream.WriteAsync(buffer, cts.Token).AsTask().Wait();
                return true;
            }
            catch
            {
                // Manche Bluetooth-Stacks nehmen nur SetOutputReport an
                try { return HidNative.HidD_SetOutputReport(_handle, buffer, buffer.Length); }
                catch { return false; }
            }
        }

        /// <summary>Einen Input-Report lesen (null bei Zeitüberschreitung). Ein offener Lesevorgang wird weiterverwendet.</summary>
        public byte[]? Read(int timeoutMs)
        {
            try
            {
                if (_pending == null)
                {
                    _pendingBuffer = new byte[Math.Max(_inLen, 22)];
                    _pending = _stream.ReadAsync(_pendingBuffer, 0, _pendingBuffer.Length);
                }
                if (!_pending.Wait(timeoutMs))
                    return null;
                var n = _pending.Result;
                _pending = null;
                return n > 0 ? _pendingBuffer[..n] : null;
            }
            catch
            {
                _pending = null;
                return null;
            }
        }

        public byte[]? ReadUntil(byte reportId, int timeoutMs)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                var left = (int)Math.Max(1, (end - DateTime.UtcNow).TotalMilliseconds);
                var r = Read(left);
                if (r != null && r[0] == reportId)
                    return r;
            }
            return null;
        }

        public void Dispose()
        {
            try { _stream.Dispose(); } catch { }
            try { _handle.Dispose(); } catch { }
        }
    }
}
