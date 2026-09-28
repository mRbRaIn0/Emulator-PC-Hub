using System.Runtime.InteropServices;
using EmulatorPCHub.Core.Logging;

namespace EmulatorPCHub.Core.Audio;

/// <summary>Eine Stimme im Mixer. Schreibt additiv in einen Stereo-Float-Puffer.</summary>
public interface IAudioVoice
{
    /// <summary>Mischt <paramref name="frames"/> Stereo-Frames. Gibt false zurück, wenn die Stimme fertig ist.</summary>
    bool Mix(Span<float> stereo, int frames);
}

/// <summary>Monoklang (Samples im Bereich -1..1) mit fester Samplerate <see cref="AudioEngine.SampleRate"/>.</summary>
public sealed class SoundClip
{
    public float[] Samples { get; }
    public SoundClip(float[] samples) => Samples = samples;
    public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (double)AudioEngine.SampleRate);
}

/// <summary>
/// Kleiner Software-Mixer über die WinMM-waveOut-API (in Windows enthalten, keine Abhängigkeiten).
/// Wird für UI-Sounds des Hubs und für Musik/Effekte von Hub Kart verwendet.
/// Ist kein Audiogerät vorhanden, bleibt die Engine stumm.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    public const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BufferFrames = 735; // ~16,7 ms
    private const int BufferCount = 4;

    private readonly List<IAudioVoice> _voices = [];
    private readonly object _lock = new();
    private readonly Thread? _thread;
    private readonly AutoResetEvent _event = new(false);
    private volatile bool _running;
    private IntPtr _device;
    private readonly IntPtr[] _headers = new IntPtr[BufferCount];
    private readonly IntPtr[] _data = new IntPtr[BufferCount];
    private readonly float[] _mix = new float[BufferFrames * Channels];
    private readonly short[] _pcm = new short[BufferFrames * Channels];

    public float MasterVolume { get; set; } = 0.8f;
    public bool IsAvailable { get; }

    public static AudioEngine? Shared { get; set; }

    public AudioEngine()
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            var format = new WaveFormatEx
            {
                wFormatTag = 1, // PCM
                nChannels = Channels,
                nSamplesPerSec = SampleRate,
                wBitsPerSample = 16,
                nBlockAlign = Channels * 2,
                nAvgBytesPerSec = SampleRate * Channels * 2,
                cbSize = 0,
            };
            var result = waveOutOpen(out _device, WaveMapper, ref format,
                _event.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent);
            if (result != 0)
            {
                HubLog.Warn($"Audio: waveOutOpen fehlgeschlagen (Code {result}), Sound deaktiviert.");
                return;
            }

            var headerSize = Marshal.SizeOf<WaveHdr>();
            for (int i = 0; i < BufferCount; i++)
            {
                _data[i] = Marshal.AllocHGlobal(BufferFrames * Channels * 2);
                _headers[i] = Marshal.AllocHGlobal(headerSize);
                var hdr = new WaveHdr { lpData = _data[i], dwBufferLength = (uint)(BufferFrames * Channels * 2) };
                Marshal.StructureToPtr(hdr, _headers[i], false);
                waveOutPrepareHeader(_device, _headers[i], (uint)headerSize);
            }

            IsAvailable = true;
            _running = true;
            _thread = new Thread(Run) { IsBackground = true, Name = "HubAudio", Priority = ThreadPriority.Highest };
            _thread.Start();
        }
        catch (Exception ex)
        {
            HubLog.Warn("Audio konnte nicht initialisiert werden", ex);
        }
    }

    public void AddVoice(IAudioVoice voice)
    {
        if (!IsAvailable)
            return;
        lock (_lock)
            _voices.Add(voice);
    }

    public void RemoveVoice(IAudioVoice voice)
    {
        lock (_lock)
            _voices.Remove(voice);
    }

    public ClipVoice? Play(SoundClip? clip, float volume = 1f, float pan = 0f, float pitch = 1f)
    {
        if (clip == null || !IsAvailable || volume <= 0.001f)
            return null;
        var voice = new ClipVoice(clip, volume, pan, pitch);
        AddVoice(voice);
        return voice;
    }

    private void Run()
    {
        var headerSize = (uint)Marshal.SizeOf<WaveHdr>();
        var flagsOffset = Marshal.OffsetOf<WaveHdr>(nameof(WaveHdr.dwFlags)).ToInt32();
        for (int i = 0; i < BufferCount; i++)
        {
            Fill(i);
            waveOutWrite(_device, _headers[i], headerSize);
        }

        while (_running)
        {
            _event.WaitOne(50);
            for (int i = 0; i < BufferCount && _running; i++)
            {
                var flags = Marshal.ReadInt32(_headers[i], flagsOffset);
                if ((flags & WhdrDone) != 0)
                {
                    Fill(i);
                    waveOutWrite(_device, _headers[i], headerSize);
                }
            }
        }
    }

    private void Fill(int index)
    {
        Array.Clear(_mix);
        lock (_lock)
        {
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                bool alive;
                try
                {
                    alive = _voices[v].Mix(_mix, BufferFrames);
                }
                catch (Exception ex)
                {
                    HubLog.Warn("Audio-Stimme fehlerhaft, entfernt", ex);
                    alive = false;
                }
                if (!alive)
                    _voices.RemoveAt(v);
            }
        }

        var master = MasterVolume;
        for (int i = 0; i < _mix.Length; i++)
        {
            // weiches Clipping
            var s = _mix[i] * master;
            s = s / (1f + MathF.Abs(s) * 0.35f);
            _pcm[i] = (short)Math.Clamp(s * 32767f, -32768f, 32767f);
        }
        Marshal.Copy(_pcm, 0, _data[index], _pcm.Length);
    }

    public void Dispose()
    {
        if (!IsAvailable)
            return;
        _running = false;
        _event.Set();
        _thread?.Join(500);
        try
        {
            waveOutReset(_device);
            var headerSize = (uint)Marshal.SizeOf<WaveHdr>();
            for (int i = 0; i < BufferCount; i++)
            {
                waveOutUnprepareHeader(_device, _headers[i], headerSize);
                Marshal.FreeHGlobal(_headers[i]);
                Marshal.FreeHGlobal(_data[i]);
            }
            waveOutClose(_device);
        }
        catch
        {
            // Beim Beenden ignorieren.
        }
    }

    // ---- WinMM ----
    private const uint WaveMapper = 0xFFFFFFFF;
    private const uint CallbackEvent = 0x00050000;
    private const int WhdrDone = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hWaveOut, uint uDeviceID, ref WaveFormatEx lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
    [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
    [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
    [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
    [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr hWaveOut);
    [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr hWaveOut);
}

/// <summary>Spielt einen <see cref="SoundClip"/> einmal ab (mit Tonhöhe und Panorama).</summary>
public sealed class ClipVoice : IAudioVoice
{
    private readonly SoundClip _clip;
    private double _pos;
    public float Volume { get; set; }
    public float Pan { get; set; }
    public float Pitch { get; set; }
    public bool Stopped { get; set; }

    public ClipVoice(SoundClip clip, float volume, float pan, float pitch)
    {
        _clip = clip;
        Volume = volume;
        Pan = pan;
        Pitch = pitch;
    }

    public bool Mix(Span<float> stereo, int frames)
    {
        if (Stopped)
            return false;
        var samples = _clip.Samples;
        var left = Volume * Math.Clamp(1f - Pan, 0f, 1f);
        var right = Volume * Math.Clamp(1f + Pan, 0f, 1f);
        for (int i = 0; i < frames; i++)
        {
            var idx = (int)_pos;
            if (idx >= samples.Length - 1)
                return false;
            var frac = (float)(_pos - idx);
            var s = samples[idx] + (samples[idx + 1] - samples[idx]) * frac;
            stereo[i * 2] += s * left;
            stereo[i * 2 + 1] += s * right;
            _pos += Pitch;
        }
        return true;
    }
}
