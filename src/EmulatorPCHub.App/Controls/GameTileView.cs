using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI.ViewModels;
using Windows.UI;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Große quadratische Spiele-Kachel (Plan 3.1). Ohne eigenes Cover wird ein eigenes Artwork aus
/// Plattformfarbe, Monogramm und Titel erzeugt – es werden keine Nintendo-Assets mitgeliefert.
/// </summary>
public sealed class GameTileView : Grid
{
    private readonly Border _selection;
    private readonly Border _card;
    private bool _selected;

    public GameTileViewModel Tile { get; }
    public event Action<GameTileView>? Clicked;

    public GameTileView(GameTileViewModel tile, double size)
    {
        Tile = tile;
        Width = size + 16;
        Height = size + 16;
        Margin = new Thickness(6, 0, 6, 0);

        _selection = new Border
        {
            BorderThickness = new Thickness(5),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
        };
        _card = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            Background = Gradient(tile.Accent, tile.AccentDark),
            Child = BuildContent(tile, size),
        };
        Children.Add(_card);
        Children.Add(_selection);

        ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(140) };
        SizeChanged += (_, _) => CenterPoint = new Vector3((float)ActualWidth / 2, (float)ActualHeight / 2, 0);
        PointerPressed += (_, e) =>
        {
            Clicked?.Invoke(this);
            e.Handled = true;
        };
    }

    public bool IsSelectedTile
    {
        get => _selected;
        set
        {
            _selected = value;
            _selection.BorderBrush = new SolidColorBrush(value ? Color.FromArgb(255, 0, 195, 227) : Colors.Transparent);
            Scale = value ? new Vector3(1.06f, 1.06f, 1) : Vector3.One;
            Canvas.SetZIndex(this, value ? 10 : 0);
        }
    }

    private static UIElement BuildContent(GameTileViewModel tile, double size)
    {
        var grid = new Grid();
        if (tile.HasCover)
        {
            grid.Children.Add(new Image
            {
                // ohne Bild-Cache, damit ein geändertes Cover sofort sichtbar ist
                Source = new BitmapImage(new Uri(tile.CoverPath!)) { CreateOptions = BitmapCreateOptions.IgnoreImageCache },
                Stretch = Stretch.UniformToFill,
            });
        }
        else
        {
            // Eigenes Artwork
            if (tile.Game.Special != SpecialPage.None)
                grid.Children.Add(Checkered(size));
            grid.Children.Add(new TextBlock
            {
                Text = tile.Monogram,
                FontSize = size * 0.30,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, size * 0.12),
            });
            grid.Children.Add(new TextBlock
            {
                Text = tile.Title,
                FontSize = size * 0.085,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(size * 0.07, 0, size * 0.07, size * 0.16),
            });
        }

        // Plattform-Badge
        grid.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 3, 10, 4),
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new TextBlock
            {
                Text = tile.PlatformShort,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
            },
        });

        if (tile.IsFavorite)
        {
            grid.Children.Add(new TextBlock
            {
                Text = "★",
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 214, 0)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 12, 0),
            });
        }

        if (tile.Game.IsPlaceholder)
        {
            grid.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10, 6, 10, 6),
                Child = new TextBlock
                {
                    Text = Loc.T("Einrichtung nötig"),
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Colors.White),
                },
            });
        }
        return grid;
    }

    /// <summary>Zielflaggen-Muster für die Rennspiel-Kacheln (eigenes Motiv).</summary>
    private static UIElement Checkered(double size)
    {
        var canvas = new Canvas { Width = size, Height = size, Opacity = 0.35 };
        var cell = size / 16;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 16; col++)
            {
                if ((row + col) % 2 != 0)
                    continue;
                var r = new Rectangle { Width = cell, Height = cell, Fill = new SolidColorBrush(Colors.White) };
                Canvas.SetLeft(r, col * cell);
                Canvas.SetTop(r, row * cell);
                canvas.Children.Add(r);
            }
        }
        return canvas;
    }

    public static LinearGradientBrush Gradient(string from, string to) => new()
    {
        StartPoint = new Windows.Foundation.Point(0, 0),
        EndPoint = new Windows.Foundation.Point(1, 1),
        GradientStops =
        {
            new GradientStop { Color = AvatarView.ParseColor(from), Offset = 0 },
            new GradientStop { Color = AvatarView.ParseColor(to), Offset = 1 },
        },
    };
}
