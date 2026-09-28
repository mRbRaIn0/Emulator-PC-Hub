using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using EmulatorPCHub.Core.Models;
using Windows.UI;

namespace EmulatorPCHub.App.Controls;

/// <summary>Eigener, Mii-ähnlicher Avatar (Plan Abschnitt 21) – aus einfachen Formen gezeichnet.</summary>
public sealed class AvatarView : UserControl
{
    private readonly Canvas _canvas = new() { Width = 100, Height = 100 };

    public static readonly DependencyProperty SpecProperty = DependencyProperty.Register(
        nameof(Spec), typeof(AvatarSpec), typeof(AvatarView), new PropertyMetadata(null, (d, _) => ((AvatarView)d).Rebuild()));

    public AvatarSpec? Spec
    {
        get => (AvatarSpec?)GetValue(SpecProperty);
        set => SetValue(SpecProperty, value);
    }

    public AvatarView()
    {
        var grid = new Grid
        {
            Width = 100,
            Height = 100,
            Children = { _canvas },
        };
        // Runder Ausschnitt über die Composition-Ebene
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(grid);
        var ellipse = visual.Compositor.CreateEllipseGeometry();
        ellipse.Center = new System.Numerics.Vector2(50, 50);
        ellipse.Radius = new System.Numerics.Vector2(50, 50);
        visual.Clip = visual.Compositor.CreateGeometricClip(ellipse);
        Content = new Viewbox { Child = grid };
        Rebuild();
    }

    public void Rebuild()
    {
        var s = Spec ?? new AvatarSpec();
        _canvas.Children.Clear();

        Add(new Ellipse { Width = 100, Height = 100, Fill = Brush(s.Background) }, 0, 0);
        // Oberkörper
        Add(new Ellipse { Width = 76, Height = 50, Fill = Brush(s.Shirt) }, 12, 76);
        // Haare hinten
        if (s.HairStyle is 2 or 3)
            Add(new Rectangle { Width = 56, Height = 44, RadiusX = 20, RadiusY = 20, Fill = Brush(s.Hair) }, 22, 30);
        // Gesicht
        Add(new Ellipse { Width = 50, Height = 58, Fill = Brush(s.Skin) }, 25, 22);
        // Ohren
        Add(new Ellipse { Width = 10, Height = 14, Fill = Brush(s.Skin) }, 20, 44);
        Add(new Ellipse { Width = 10, Height = 14, Fill = Brush(s.Skin) }, 70, 44);
        // Haare oben
        switch (s.HairStyle)
        {
            case 0:
                Add(new Ellipse { Width = 52, Height = 22, Fill = Brush(s.Hair) }, 24, 18);
                break;
            case 1:
                Add(new Ellipse { Width = 56, Height = 30, Fill = Brush(s.Hair) }, 22, 16);
                Add(new Ellipse { Width = 22, Height = 16, Fill = Brush(s.Hair) }, 26, 30);
                break;
            case 2:
                Add(new Ellipse { Width = 58, Height = 32, Fill = Brush(s.Hair) }, 21, 14);
                break;
            default:
                Add(new Ellipse { Width = 58, Height = 30, Fill = Brush(s.Hair) }, 21, 14);
                Add(new Ellipse { Width = 18, Height = 18, Fill = Brush(s.Hair) }, 41, 6);
                break;
        }
        // Augen
        var eyeColor = new SolidColorBrush(Color.FromArgb(255, 40, 30, 30));
        switch (s.Eyes)
        {
            case 1:
                Add(new Rectangle { Width = 9, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = eyeColor }, 36, 50);
                Add(new Rectangle { Width = 9, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = eyeColor }, 55, 50);
                break;
            case 2:
                Add(new Ellipse { Width = 9, Height = 11, Fill = new SolidColorBrush(Colors.White) }, 35, 45);
                Add(new Ellipse { Width = 9, Height = 11, Fill = new SolidColorBrush(Colors.White) }, 56, 45);
                Add(new Ellipse { Width = 5, Height = 6, Fill = eyeColor }, 37, 49);
                Add(new Ellipse { Width = 5, Height = 6, Fill = eyeColor }, 58, 49);
                break;
            default:
                Add(new Ellipse { Width = 6, Height = 8, Fill = eyeColor }, 37, 47);
                Add(new Ellipse { Width = 6, Height = 8, Fill = eyeColor }, 57, 47);
                break;
        }
        // Wangen
        var cheek = new SolidColorBrush(Color.FromArgb(60, 255, 90, 90));
        Add(new Ellipse { Width = 8, Height = 5, Fill = cheek }, 30, 58);
        Add(new Ellipse { Width = 8, Height = 5, Fill = cheek }, 62, 58);
        // Mund
        var mouth = new SolidColorBrush(Color.FromArgb(255, 150, 60, 60));
        switch (s.Mouth)
        {
            case 1:
                Add(new Ellipse { Width = 10, Height = 8, Fill = mouth }, 45, 63);
                break;
            case 2:
                Add(new Rectangle { Width = 14, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = mouth }, 43, 65);
                break;
            default:
                var path = new Microsoft.UI.Xaml.Shapes.Path
                {
                    Stroke = mouth,
                    StrokeThickness = 3,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Data = new PathGeometry
                    {
                        Figures =
                        {
                            new PathFigure
                            {
                                StartPoint = new Windows.Foundation.Point(42, 63),
                                Segments = { new QuadraticBezierSegment { Point1 = new Windows.Foundation.Point(50, 71), Point2 = new Windows.Foundation.Point(58, 63) } },
                            },
                        },
                    },
                };
                _canvas.Children.Add(path);
                break;
        }
    }

    private void Add(UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        _canvas.Children.Add(e);
    }

    public static SolidColorBrush Brush(string hex) => new(ParseColor(hex));

    public static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex;
        var v = Convert.ToUInt32(hex, 16);
        return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }
}
