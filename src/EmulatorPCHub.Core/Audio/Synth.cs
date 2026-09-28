namespace EmulatorPCHub.Core.Audio;

public enum Wave
{
    Sine,
    Square,
    Pulse25,
    Triangle,
    Saw,
    Noise,
}

/// <summary>Erzeugt kurze Klänge prozedural – so werden keine fremden Sound-Assets benötigt.</summary>
public static class Synth
{
    private static readonly Random Rng = new(1234);

    public static float Osc(Wave wave, double phase)
    {
        var p = phase - Math.Floor(phase);
        return wave switch
        {
            Wave.Sine => (float)Math.Sin(p * Math.PI * 2),
            Wave.Square => p < 0.5 ? 0.6f : -0.6f,
            Wave.Pulse25 => p < 0.25 ? 0.6f : -0.6f,
            Wave.Triangle => (float)(p < 0.5 ? p * 4 - 1 : 3 - p * 4),
            Wave.Saw => (float)(p * 2 - 1) * 0.6f,
            Wave.Noise => (float)(Rng.NextDouble() * 2 - 1) * 0.5f,
            _ => 0f,
        };
    }

    public static double NoteFrequency(int midiNote) => 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);

    /// <summary>Ton mit linearer Frequenzänderung und ADSR-artiger Hüllkurve.</summary>
    public static float[] Tone(Wave wave, double freqStart, double freqEnd, double seconds,
        double attack = 0.005, double release = 0.08, float volume = 0.5f, double vibrato = 0)
    {
        var n = (int)(seconds * AudioEngine.SampleRate);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            var t = i / (double)AudioEngine.SampleRate;
            var k = t / seconds;
            var f = freqStart + (freqEnd - freqStart) * k;
            if (vibrato > 0)
                f *= 1 + Math.Sin(t * Math.PI * 2 * 6) * vibrato;
            phase += f / AudioEngine.SampleRate;
            data[i] = Osc(wave, phase) * Envelope(t, seconds, attack, release) * volume;
        }
        return data;
    }

    public static float Envelope(double t, double length, double attack, double release)
    {
        if (t < attack)
            return (float)(t / attack);
        if (t > length - release)
            return (float)Math.Max(0, (length - t) / release);
        return 1f;
    }

    /// <summary>Folge von Noten (MIDI-Nummern), z. B. für Jingles.</summary>
    public static float[] Arpeggio(Wave wave, IReadOnlyList<int> notes, double noteSeconds, float volume = 0.4f, double tail = 0.15)
    {
        var total = notes.Count * noteSeconds + tail;
        var result = new float[(int)(total * AudioEngine.SampleRate)];
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i] < 0)
                continue;
            var f = NoteFrequency(notes[i]);
            var len = i == notes.Count - 1 ? noteSeconds + tail : noteSeconds * 1.2;
            var tone = Tone(wave, f, f, len, 0.004, Math.Min(0.12, len * 0.6), volume);
            MixInto(result, tone, (int)(i * noteSeconds * AudioEngine.SampleRate));
        }
        return result;
    }

    public static void MixInto(float[] target, float[] source, int offset, float gain = 1f)
    {
        for (int i = 0; i < source.Length && offset + i < target.Length; i++)
        {
            if (offset + i >= 0)
                target[offset + i] += source[i] * gain;
        }
    }

    public static float[] Concat(params float[][] parts)
    {
        var result = new float[parts.Sum(p => p.Length)];
        var o = 0;
        foreach (var p in parts)
        {
            p.CopyTo(result, o);
            o += p.Length;
        }
        return result;
    }

    /// <summary>Einfacher Tiefpass (für Rauschen, z. B. Motor/Wind).</summary>
    public static float[] LowPass(float[] data, float amount)
    {
        var y = 0f;
        var result = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            y += (data[i] - y) * amount;
            result[i] = y;
        }
        return result;
    }
}

/// <summary>Die UI-Sounds des Hubs (Plan Abschnitt 22) – eigene, synthetisierte Klänge.</summary>
public sealed class UiSounds
{
    public SoundClip Move { get; } = new(Synth.Tone(Wave.Triangle, 1760, 1900, 0.035, 0.002, 0.025, 0.25f));
    public SoundClip Select { get; } = new(Synth.Concat(
        Synth.Tone(Wave.Square, 988, 988, 0.04, 0.002, 0.02, 0.18f),
        Synth.Tone(Wave.Square, 1480, 1480, 0.07, 0.002, 0.05, 0.18f)));
    public SoundClip Back { get; } = new(Synth.Tone(Wave.Triangle, 900, 520, 0.09, 0.002, 0.05, 0.3f));
    public SoundClip Toggle { get; } = new(Synth.Tone(Wave.Pulse25, 1320, 1320, 0.05, 0.002, 0.04, 0.15f));
    public SoundClip Error { get; } = new(Synth.Tone(Wave.Square, 180, 150, 0.22, 0.004, 0.08, 0.2f));
    public SoundClip Favorite { get; } = new(Synth.Arpeggio(Wave.Triangle, [84, 88, 91, 96], 0.045, 0.3f, 0.1));
    public SoundClip Launch { get; } = new(Synth.Arpeggio(Wave.Square, [72, 76, 79, 84, 88], 0.06, 0.16f, 0.25));
    public SoundClip Startup { get; } = new(BuildStartup());
    public SoundClip Connect { get; } = new(Synth.Arpeggio(Wave.Sine, [79, 86], 0.07, 0.35f, 0.1));

    private static float[] BuildStartup()
    {
        // Kurzer, eigener Start-Jingle (C-Dur, zwei Stimmen).
        var lead = Synth.Arpeggio(Wave.Square, [72, 79, 76, 84], 0.11, 0.13f, 0.4);
        var bass = Synth.Arpeggio(Wave.Triangle, [48, 55, 52, 60], 0.11, 0.35f, 0.4);
        var sparkle = Synth.Arpeggio(Wave.Sine, [-1, -1, -1, -1, 96, 100, 103, 108], 0.055, 0.12f, 0.2);
        var mix = new float[Math.Max(lead.Length, sparkle.Length)];
        Synth.MixInto(mix, lead, 0);
        Synth.MixInto(mix, bass, 0);
        Synth.MixInto(mix, sparkle, 0);
        return mix;
    }
}
