using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Hintergrund des dunklen Designs: Farbverlauf mit zwei leuchtenden Wellenkanten. Farbe wählbar (Standard: Grau),
/// die Wellen bewegen sich nur, wenn die Animation eingeschaltet ist (Standard: aus).
/// </summary>
public sealed class BackgroundView : UserControl
{
    /// <summary>Farb-ID → Farbton (null = Grau).</summary>
    public static readonly (string Id, string Label, double? Hue)[] Palettes =
    [
        ("grau", "Grau", null), ("rot", "Rot", 355), ("blau", "Blau", 228), ("tuerkis", "Türkis", 190),
        ("gruen", "Grün", 140), ("violett", "Violett", 270), ("pink", "Pink", 322),
    ];

    private const string Ridge = "M-220 336L-20 250C120 190 260 165 420 185C640 212 800 300 1050 345C1300 385 1500 300 1700 235L1900 170";
    private const string Valley = "M-220 564L-20 600C200 640 450 665 700 662C1000 658 1250 640 1700 500L1900 438";

    private readonly Canvas _canvas = new() { Width = 1672, Height = 941 };
    private Storyboard? _storyboard;

    public BackgroundView()
    {
        IsHitTestVisible = false;
        var grid = new Grid();
        grid.Children.Add(new Viewbox { Stretch = Stretch.UniformToFill, Child = _canvas });
        grid.SizeChanged += (_, e) => grid.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
        Content = grid;
    }

    public void Apply(string colorId, bool animate)
    {
        var hue = Palettes.FirstOrDefault(p => p.Id == colorId).Hue;
        _storyboard?.Stop();
        _canvas.Children.Clear();
        TranslateTransform ridgeMove = new(), valleyMove = new(); // je Aufruf neu: ein Transform gehört nur einem Element

        _canvas.Children.Add(new Rectangle
        {
            Width = 1672,
            Height = 941,
            Fill = Gradient(new Point(0.3, 0), new Point(0.7, 1),
                (0, Hsl(hue, 0.42, 0.20)), (0.4, Hsl(hue, 0.28, 0.17)), (0.8, Hsl(hue, 0.14, 0.12)), (1, Hsl(hue, 0.05, 0.08))),
        });
        _canvas.Children.Add(Wave(Ridge, ridgeMove, Gradient(new Point(0, 0), new Point(0, 1), (0, Hsl(hue, 0.7, 0.50, 0.30)), (1, Hsl(hue, 0.7, 0.35, 0))), hue, 0.30));
        _canvas.Children.Add(Wave(Valley, valleyMove, Gradient(new Point(0, 0), new Point(0, 1), (0, Hsl(hue, 0.3, 0.05, 0.10)), (1, Hsl(hue, 0.3, 0.02, 0.80))), hue, 0.22));

        if (!animate)
        {
            _storyboard = null;
            return;
        }
        _storyboard = new Storyboard();
        Move(_storyboard, ridgeMove, "X", -70, 16);
        Move(_storyboard, valleyMove, "Y", 16, 11);
        _storyboard.Begin();
    }

    private static Canvas Wave(string d, TranslateTransform move, Brush fill, double? hue, double glow)
    {
        // Eine Geometrie darf nur einem Pfad gehören – darum je Pfad neu erzeugen.
        static Geometry Geo(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);
        var edge = Gradient(new Point(0, 0), new Point(1, 0),
            (0, Hsl(hue, 1, 0.75, 0)), (0.3, Hsl(hue, 1, 0.82)), (0.6, Colors.White), (1, Hsl(hue, 1, 0.75, 0.2)));
        var layer = new Canvas { RenderTransform = move };
        layer.Children.Add(new ShapePath { Data = Geo(d + "L1900 1100L-220 1100Z"), Fill = fill });
        layer.Children.Add(new ShapePath { Data = Geo(d), Stroke = edge, StrokeThickness = 14, Opacity = glow }); // Leuchten
        layer.Children.Add(new ShapePath { Data = Geo(d), Stroke = edge, StrokeThickness = 2.2 });
        return layer;
    }

    private static void Move(Storyboard sb, TranslateTransform target, string property, double to, double seconds)
    {
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromSeconds(seconds),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        sb.Children.Add(anim);
    }

    private static LinearGradientBrush Gradient(Point start, Point end, params (double Offset, Windows.UI.Color Color)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (offset, color) in stops)
            brush.GradientStops.Add(new GradientStop { Offset = offset, Color = color });
        return brush;
    }

    /// <summary>HSL → Farbe; Farbton null = Grau (Sättigung 0).</summary>
    private static Windows.UI.Color Hsl(double? hue, double saturation, double lightness, double alpha = 1)
    {
        var s = hue == null ? 0 : saturation;
        var h = (hue ?? 0) / 360.0;
        double q = lightness < 0.5 ? lightness * (1 + s) : lightness + s - lightness * s, p = 2 * lightness - q;
        double Channel(double t)
        {
            t = (t % 1 + 1) % 1;
            return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p;
        }
        byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255, MidpointRounding.AwayFromZero);
        return Windows.UI.Color.FromArgb(B(alpha), B(Channel(h + 1 / 3.0)), B(Channel(h)), B(Channel(h - 1 / 3.0)));
    }
}
