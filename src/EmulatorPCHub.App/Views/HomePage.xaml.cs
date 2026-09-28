using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Homescreen (Plan 3.1): horizontale Spiele-Kacheln, ausgewählte Kachel vergrößert mit Hintergrund passend
/// zum Spiel, darunter die runden Buttons News | Library | Controllers | Mods | Komponenten | Settings | Power.
/// </summary>
public sealed partial class HomePage : Page, IHubPage
{
    private readonly HomeViewModel _vm = new(App.Hub);
    private readonly List<GameTileView> _tiles = [];
    private readonly List<(Button button, TextBlock label)> _nav = [];
    private int _tileIndex;
    private int _navIndex;
    private bool _navZone;
    private static string? _lastSelectedId;

    public string Hints => "Ⓐ Starten   Ⓧ Favorit   Ⓨ Optionen   ⊕ Einstellungen";

    public HomePage()
    {
        InitializeComponent();
        BuildNav();
        App.Hub.Library.Changed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            if (ReferenceEquals(MainWindow.Current.CurrentPage, this))
                Reload();
        });
    }

    public void OnShown() => Reload();

    private void Reload()
    {
        _vm.Reload();
        TilesPanel.Children.Clear();
        _tiles.Clear();
        foreach (var tile in _vm.Tiles)
        {
            var view = new GameTileView(tile, (double)Application.Current.Resources["HubTileSize"]);
            view.Clicked += OnTileClicked;
            _tiles.Add(view);
            TilesPanel.Children.Add(view);
        }
        var idx = _tiles.FindIndex(t => t.Tile.Id == _lastSelectedId);
        SelectTile(idx >= 0 ? idx : 0, sound: false);
        EmptyHint.Text = _vm.EmptyHint;
        UpdateZoneVisuals();
    }

    private void BuildNav()
    {
        var items = new (string glyph, string label, Action action)[]
        {
            ("", "News", () => MainWindow.Current.Navigate(typeof(NewsPage))),
            ("", "Library", () => MainWindow.Current.Navigate(typeof(LibraryPage))),
            ("", "Profile", () => MainWindow.Current.Navigate(typeof(ProfilesPage))),
            ("", "Mii", () => MainWindow.Current.Navigate(typeof(MiiPage))),
            ("", "Spielstände", () => MainWindow.Current.Navigate(typeof(SavesPage))),
            ("", "Statistik", () => MainWindow.Current.Navigate(typeof(StatisticsPage))),
            ("", "Controller", () => MainWindow.Current.Navigate(typeof(ControllersPage))),
            ("", "Mods", () => MainWindow.Current.Navigate(typeof(ModsPage))),
            ("", "Komponenten", () => MainWindow.Current.Navigate(typeof(DownloadsPage))),
            ("", "Settings", () => MainWindow.Current.Navigate(typeof(SettingsPage))),
            ("", "Power", () => _ = PowerMenu.ShowAsync()),
        };
        for (int i = 0; i < items.Length; i++)
        {
            var (glyph, label, action) = items[i];
            var index = i;
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["HubRoundButton"],
                Content = new FontIcon { Glyph = glyph, FontSize = 26 },
                IsTabStop = false,
            };
            button.Click += (_, _) =>
            {
                _navZone = true;
                _navIndex = index;
                UpdateZoneVisuals();
                action();
            };
            var text = new TextBlock
            {
                Text = label,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["HubAccentBrush"],
                Opacity = 0,
                Margin = new Thickness(0, 6, 0, 0),
            };
            var panel = new StackPanel { Children = { button, text } };
            NavPanel.Children.Add(panel);
            _nav.Add((button, text));
        }
    }

    private void SelectTile(int index, bool sound = true)
    {
        if (_tiles.Count == 0)
            return;
        index = Math.Clamp(index, 0, _tiles.Count - 1);
        if (sound && index != _tileIndex)
            MainWindow.Current.Sounds.Play(UiSound.Move);
        _tileIndex = index;
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].IsSelectedTile = !_navZone && i == index;
        var tile = _tiles[index].Tile;
        _lastSelectedId = tile.Id;
        _vm.Selected = tile;
        UpdateInfo(tile);
        ScrollIntoView(index);
    }

    private void UpdateInfo(GameTileViewModel tile)
    {
        SelectedTitle.Text = tile.Title;
        InfoPlatform.Text = tile.PlatformName;
        InfoPreset.Text = tile.IsSpecial || tile.PresetName != "Standard" ? $"Preset: {tile.PresetName}" : "Standard";
        InfoPlaytime.Text = tile.PlayTimeText;
        if (!string.IsNullOrEmpty(tile.Status))
        {
            InfoLastLabel.Text = "Status";
            InfoLast.Text = tile.Status;
        }
        else
        {
            InfoLastLabel.Text = "Zuletzt";
            InfoLast.Text = string.IsNullOrEmpty(tile.LastPlayedText) ? "–" : tile.LastPlayedText;
        }
        InfoEmulator.Text = EmulatorName(tile);
        ControllerAdviceView.FillCompact(InfoControllers, ControllerAdviceView.For(tile.Game));
        StartButtonText.Text = tile.IsSpecial ? "Öffnen" : "Starten";
        MainWindow.Current.SetBackdrop(tile.Accent, tile.AccentDark, tile.BackgroundPath);
    }

    /// <summary>Emulator bzw. PC-Port, mit dem das Spiel gestartet würde (gleiche Auswahl wie beim Start).</summary>
    private static string EmulatorName(GameTileViewModel tile)
    {
        try
        {
            var name = App.Hub.AdapterFor(tile.Game, App.Hub.Presets.Active(tile.Game)).DisplayName;
            return name.Replace(" (Switch)", "");
        }
        catch (Exception)
        {
            return "–";
        }
    }

    private void ScrollIntoView(int index)
    {
        if (index < 0 || index >= _tiles.Count)
            return;
        var tileWidth = _tiles[index].Width + 12;
        var target = index * tileWidth - (TileScroller.ViewportWidth - tileWidth) / 2;
        TileScroller.ChangeView(Math.Max(0, target), null, null, disableAnimation: false);
    }

    private void UpdateZoneVisuals()
    {
        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].IsSelectedTile = !_navZone && i == _tileIndex;
        for (int i = 0; i < _nav.Count; i++)
        {
            var selected = _navZone && i == _navIndex;
            _nav[i].button.BorderBrush = selected ? (Brush)Application.Current.Resources["HubAccentBrush"] : null;
            _nav[i].button.BorderThickness = new Thickness(selected ? 4 : 0);
            _nav[i].label.Opacity = selected ? 1 : 0;
        }
    }

    private void OnTileClicked(GameTileView view)
    {
        var index = _tiles.IndexOf(view);
        if (_navZone)
        {
            _navZone = false;
            UpdateZoneVisuals();
        }
        if (index == _tileIndex)
            ActivateSelected();
        else
            SelectTile(index);
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => ActivateSelected();

    private void ActivateSelected()
    {
        if (_tiles.Count == 0)
            return;
        var game = _tiles[_tileIndex].Tile.Game;
        MainWindow.Current.Sounds.Play(UiSound.Select);
        switch (game.Special)
        {
            case SpecialPage.MarioKartWii:
                MainWindow.Current.Navigate(typeof(MarioKartWiiPage));
                break;
            case SpecialPage.MarioKart8Deluxe:
                MainWindow.Current.Navigate(typeof(MarioKart8DeluxePage));
                break;
            default:
                _ = MainWindow.Current.LaunchAsync(game, App.Hub.Presets.Active(game));
                break;
        }
    }

    public bool HandleNav(NavAction action)
    {
        switch (action)
        {
            case NavAction.Left when !_navZone:
                SelectTile(_tileIndex - 1);
                return true;
            case NavAction.Right when !_navZone:
                SelectTile(_tileIndex + 1);
                return true;
            case NavAction.Left:
                _navIndex = Math.Max(0, _navIndex - 1);
                MainWindow.Current.Sounds.Play(UiSound.Move);
                UpdateZoneVisuals();
                return true;
            case NavAction.Right:
                _navIndex = Math.Min(_nav.Count - 1, _navIndex + 1);
                MainWindow.Current.Sounds.Play(UiSound.Move);
                UpdateZoneVisuals();
                return true;
            case NavAction.Down when !_navZone:
                _navZone = true;
                MainWindow.Current.Sounds.Play(UiSound.Move);
                UpdateZoneVisuals();
                return true;
            case NavAction.Up when _navZone:
                _navZone = false;
                MainWindow.Current.Sounds.Play(UiSound.Move);
                UpdateZoneVisuals();
                return true;
            case NavAction.Up or NavAction.Down:
                return true;
            case NavAction.Accept when _navZone:
                MainWindow.Current.Sounds.Play(UiSound.Select);
                (Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(_nav[_navIndex].button)
                    as Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)?.Invoke();
                return true;
            case NavAction.Accept:
                ActivateSelected();
                return true;
            case NavAction.X when !_navZone && _tiles.Count > 0:
                var game = _tiles[_tileIndex].Tile.Game;
                App.Hub.Library.ToggleFavorite(game);
                MainWindow.Current.Sounds.Play(game.IsFavorite ? UiSound.Favorite : UiSound.Toggle);
                MainWindow.Current.ShowToast(game.IsFavorite ? $"★ {game.Title} ist jetzt Favorit" : $"{game.Title} aus Favoriten entfernt");
                return true;
            case NavAction.Y when !_navZone && _tiles.Count > 0:
                OpenOptions();
                return true;
            case NavAction.Back:
                if (_navZone)
                {
                    _navZone = false;
                    UpdateZoneVisuals();
                }
                return true;
            case NavAction.Home:
                return true;
            default:
                return false;
        }
    }

    private void OpenOptions()
    {
        var game = _tiles[_tileIndex].Tile.Game;
        MainWindow.Current.Sounds.Play(UiSound.Select);
        switch (game.Special)
        {
            case SpecialPage.MarioKartWii:
                MainWindow.Current.Navigate(typeof(MarioKartWiiPage));
                break;
            case SpecialPage.MarioKart8Deluxe:
                MainWindow.Current.Navigate(typeof(MarioKart8DeluxePage));
                break;
            default:
                MainWindow.Current.Navigate(typeof(GamePage), game.Id);
                break;
        }
    }
}
