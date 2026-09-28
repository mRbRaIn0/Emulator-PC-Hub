using EmulatorPCHub.Core.Audio;
using EmulatorPCHub.Core.Config;

namespace EmulatorPCHub.App.Services;

public enum UiSound
{
    Move,
    Select,
    Back,
    Toggle,
    Error,
    Favorite,
    Launch,
    Startup,
    Connect,
}

/// <summary>UI-Sounds des Hubs (eigene, synthetisierte Klänge – keine fremden Assets).</summary>
public sealed class SoundService : IDisposable
{
    private readonly ConfigService _config;
    private readonly AudioEngine _engine = new();
    private readonly UiSounds _sounds = new();

    public SoundService(ConfigService config)
    {
        _config = config;
    }

    public void Play(UiSound sound)
    {
        if (!_config.Current.Ui.Sounds || !_engine.IsAvailable)
            return;
        _engine.MasterVolume = (float)_config.Current.Ui.SoundVolume;
        var clip = sound switch
        {
            UiSound.Move => _sounds.Move,
            UiSound.Select => _sounds.Select,
            UiSound.Back => _sounds.Back,
            UiSound.Toggle => _sounds.Toggle,
            UiSound.Error => _sounds.Error,
            UiSound.Favorite => _sounds.Favorite,
            UiSound.Launch => _sounds.Launch,
            UiSound.Startup => _sounds.Startup,
            UiSound.Connect => _sounds.Connect,
            _ => null,
        };
        _engine.Play(clip);
    }

    public void Dispose() => _engine.Dispose();
}
