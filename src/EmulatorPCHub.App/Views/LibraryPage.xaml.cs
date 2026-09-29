using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>„All Software“: komplette Bibliothek mit Plattform-Filter.</summary>
public sealed partial class LibraryPage : Page, IHubPage
{
    private static string _filter = "all";
    private readonly HomeViewModel _vm = new(App.Hub);

    public string Hints => "Ⓐ Spiel starten / Einstellungen   Ⓑ Zurück   L/R Filter wechseln";

    public LibraryPage()
    {
        InitializeComponent();
    }

    public void OnShown() => Build();

    private static readonly (string id, string label)[] Filters =
    [
        ("all", "Alle"), ("fav", "★ Favoriten"), ("recent", "Zuletzt gespielt"),
        ("gamecube", "GameCube"), ("wii", "Wii"), ("wiiu", "Wii U"), ("switch", "Switch"), ("ds", "DS"), ("3ds", "3DS"),
    ];

    private void Build()
    {
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Alle Spiele"));

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 12, 0, 12) };
        foreach (var (id, label) in Filters)
        {
            var b = Ui.Action(label, null, () =>
            {
                _filter = id;
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                Build();
            });
            if (id == _filter)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            filterRow.Children.Add(b);
        }
        Body.Children.Add(filterRow);

        var games = App.Hub.Library.Games.Where(Matches).OrderBy(g => g.DisplayTitle, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (_filter == "recent")
            games = games.Where(g => g.LastPlayed != null).OrderByDescending(g => g.LastPlayed).ToList();
        Body.Children.Add(Ui.Subtle($"{games.Count} Spiele"));

        var grid = new WrapPanelLike { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var g in games)
        {
            var tile = _vm.CreateTile(g);
            var view = new GameTileView(tile, 180);
            var button = new Button
            {
                Content = view,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 12),
                Background = null,
                BorderThickness = new Thickness(0),
                Style = (Style)Application.Current.Resources["HubButton"],
                CornerRadius = new CornerRadius(6),
            };
            ToolTipService.SetToolTip(button, g.DisplayTitle);
            button.Click += (_, _) => Open(g);
            view.Clicked += _ => Open(g);
            grid.Children.Add(button);
        }
        Body.Children.Add(grid);
    }

    private static bool Matches(GameEntry g) => _filter switch
    {
        "fav" => g.IsFavorite,
        "recent" => true,
        "gamecube" => g.Platform == HubPlatform.GameCube,
        "wii" => g.Platform == HubPlatform.Wii,
        "wiiu" => g.Platform == HubPlatform.WiiU,
        "switch" => g.Platform == HubPlatform.Switch,
        "ds" => g.Platform == HubPlatform.DS,
        "3ds" => g.Platform == HubPlatform.ThreeDS,
        _ => true,
    };

    /// <summary>Mini-Menü: „Spiel starten“ oder „Einstellungen“ (Name, Cover, Ausblenden).</summary>
    private static async void Open(GameEntry g)
    {
        MainWindow.Current.Sounds.Play(UiSound.Select);
        var info = $"{g.Platform.DisplayName()}   ·   {g.GameCode ?? "ohne ID"}" + (g.HiddenOnHome ? "   ·   im Hauptmenü ausgeblendet" : "");
        var result = await Dialogs.ShowAsync(g.DisplayTitle, info, "Spiel starten", "Abbrechen", "Einstellungen");
        if (result == ContentDialogResult.Primary)
            Start(g);
        else if (result == ContentDialogResult.Secondary)
            MainWindow.Current.Navigate(typeof(GameSettingsPage), g.Id);
    }

    private static void Start(GameEntry g)
    {
        switch (g.Special)
        {
            case SpecialPage.MarioKartWii:
                MainWindow.Current.Navigate(typeof(MarioKartWiiPage));
                break;
            case SpecialPage.MarioKart8Deluxe:
                MainWindow.Current.Navigate(typeof(MarioKart8DeluxePage));
                break;
            default:
                _ = MainWindow.Current.LaunchAsync(g, App.Hub.Presets.Active(g));
                break;
        }
    }

    public bool HandleNav(NavAction action)
    {
        if (action is not (NavAction.L or NavAction.R))
            return false;
        var idx = Array.FindIndex(Filters, f => f.id == _filter);
        idx = (idx + (action == NavAction.R ? 1 : -1) + Filters.Length) % Filters.Length;
        _filter = Filters[idx].id;
        MainWindow.Current.Sounds.Play(UiSound.Toggle);
        Build();
        return true;
    }
}
