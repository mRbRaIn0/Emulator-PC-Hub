using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.UI.ViewModels;

/// <summary>Eine große Spiele-Kachel auf dem Homescreen (Plan 3.1).</summary>
public sealed partial class GameTileViewModel : ObservableObject
{
    public GameEntry Game { get; private set; }

    public GameTileViewModel(GameEntry game, string presetName, string status)
    {
        Game = game;
        _presetName = presetName;
        _status = status;
    }

    public string Id => Game.Id;
    public string Title => Game.Title;
    public string PlatformName => Game.Platform.DisplayName();
    public string PlatformShort => Game.Platform.ShortName();
    public string Accent => Game.Special switch
    {
        SpecialPage.MarioKartWii => "#FF1E88E5",
        SpecialPage.MarioKart8Deluxe => "#FFE53935",
        _ => Game.Platform.AccentHex(),
    };
    public string AccentDark => Game.Special switch
    {
        SpecialPage.MarioKartWii => "#FF0D3C7A",
        SpecialPage.MarioKart8Deluxe => "#FF7A0E12",
        _ => Game.Platform switch
        {
            HubPlatform.GameCube => "#FF2B1E5C",
            HubPlatform.Wii => "#FF0F4470",
            HubPlatform.WiiU => "#FF0B4A55",
            HubPlatform.Switch => "#FF6E0008",
            HubPlatform.DS => "#FF3A3A3A",
            HubPlatform.ThreeDS => "#FF5C0B0E",
            _ => "#FF333333",
        },
    };
    public string? CoverPath => Game.CoverPath;
    public string? BackgroundPath => Game.BackgroundPath ?? Game.CoverPath;
    public bool HasCover => !string.IsNullOrEmpty(Game.CoverPath);
    public bool IsFavorite => Game.IsFavorite;
    public string PlayTimeText => Format.PlayTime(Game.PlayTimeSeconds);
    public string LastPlayedText => Format.LastPlayed(Game.LastPlayed);
    public bool IsSpecial => Game.Special != SpecialPage.None;
    public string Monogram
    {
        get
        {
            var words = Title.Split([' ', ':', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsLetterOrDigit(w[0])).Take(3).ToList();
            return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        }
    }

    [ObservableProperty] private string _presetName;
    [ObservableProperty] private string _status;

    public void Refresh(GameEntry game)
    {
        Game = game;
        OnPropertyChanged(string.Empty);
    }
}

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly HubServices _hub;

    public ObservableCollection<GameTileViewModel> Tiles { get; } = [];

    [ObservableProperty] private GameTileViewModel? _selected;
    [ObservableProperty] private string _clock = "";
    [ObservableProperty] private string _profileName = "";
    [ObservableProperty] private AvatarSpec _avatar = new();
    [ObservableProperty] private int _controllerCount;
    [ObservableProperty] private bool _online;
    [ObservableProperty] private string _emptyHint = "";

    public HomeViewModel(HubServices hub)
    {
        _hub = hub;
    }

    public void Reload()
    {
        var selectedId = Selected?.Id;
        var games = _hub.Library.HomeOrder();
        Tiles.Clear();
        foreach (var g in games)
            Tiles.Add(CreateTile(g));
        Selected = Tiles.FirstOrDefault(t => t.Id == selectedId) ?? Tiles.FirstOrDefault();
        ProfileName = _hub.Profiles.Active.Name;
        Avatar = _hub.Profiles.Active.Avatar;
        EmptyHint = Tiles.Count <= 2
            ? "Tipp: Unter Einstellungen → Bibliothek deine Spieleordner eintragen – eigene Dumps werden automatisch erkannt."
            : "";
    }

    public GameTileViewModel CreateTile(GameEntry g)
    {
        var preset = _hub.Presets.Active(g, g.Special switch
        {
            SpecialPage.MarioKartWii => _hub.Config.Current.MarioKartWii.Preset,
            SpecialPage.MarioKart8Deluxe => _hub.Config.Current.MarioKart8Deluxe.Preset,
            _ => null,
        });
        var status = g.IsPlaceholder ? "Eigenen Dump hinzufügen" : "";
        return new GameTileViewModel(g, preset.Name, status);
    }

    public void Tick(DateTime now) => Clock = Format.Clock(now, _hub.Config.Current.Ui.Clock24h);
}
