using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Emulation.Input;
using Windows.UI;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Schematische Controller-Grafik (eigene Zeichnung): PS5-DualSense oder Wii Remote + Nunchuk.
/// Einzelne Tasten lassen sich hervorheben – so sieht man in der Belegungstabelle, wo die Taste sitzt.
/// </summary>
public sealed class ControllerDiagram : Canvas
{
    private readonly Dictionary<string, List<Shape>> _parts = [];
    private readonly Dictionary<Shape, (Brush? fill, Brush? stroke, double thickness)> _original = [];
    private readonly Brush _body;
    private readonly Brush _button;
    private readonly Brush _line;
    private readonly Brush _text;
    private readonly Brush _accent;
    private readonly Brush _accentFill;

    private ControllerDiagram(double width, double height)
    {
        Width = width;
        Height = height;
        _body = (Brush)Application.Current.Resources["HubSurfaceAltBrush"];
        _button = (Brush)Application.Current.Resources["HubSurfaceBrush"];
        _line = Ui.SubtleBrush;
        _text = Ui.TextBrush;
        _accent = Ui.AccentBrush;
        _accentFill = new SolidColorBrush(Color.FromArgb(110, 0, 195, 227));
    }

    /// <summary>Taste(n) hervorheben; Schlüssel: <see cref="PadButton"/>-Name (DualSense) bzw. Zielname (Wii Remote).</summary>
    public void Highlight(params string?[] keys)
    {
        foreach (var (shape, o) in _original)
        {
            shape.Fill = o.fill;
            shape.Stroke = o.stroke;
            shape.StrokeThickness = o.thickness;
        }
        foreach (var key in keys)
        {
            if (key == null || !_parts.TryGetValue(key, out var shapes))
                continue;
            foreach (var s in shapes)
            {
                s.Fill = _accentFill;
                s.Stroke = _accent;
                s.StrokeThickness = 3;
            }
        }
    }

    // ------------------------------------------------------------------
    // PS5 DualSense
    // ------------------------------------------------------------------

    public static ControllerDiagram DualSense()
    {
        var d = new ControllerDiagram(440, 260);
        // Schultertasten
        d.Rect(nameof(PadButton.L2), 48, 6, 86, 22, 10, "L2");
        d.Rect(nameof(PadButton.L1), 48, 32, 86, 16, 8, "L1");
        d.Rect(nameof(PadButton.R2), 306, 6, 86, 22, 10, "R2");
        d.Rect(nameof(PadButton.R1), 306, 32, 86, 16, 8, "R1");
        // Griffe + Gehäuse
        d.Body(new Ellipse(), 22, 110, 130, 145);
        d.Body(new Ellipse(), 288, 110, 130, 145);
        d.Body(new Rectangle { RadiusX = 56, RadiusY = 56 }, 24, 50, 392, 150);
        // Touchpad, Create, Options, PS
        d.Rect(null, 150, 58, 140, 72, 12, null);
        d.Rect(nameof(PadButton.Back), 126, 60, 14, 26, 7, null);
        d.Rect(nameof(PadButton.Start), 300, 60, 14, 26, 7, null);
        d.Circle(nameof(PadButton.Guide), 220, 170, 11, "PS", 9);
        // Steuerkreuz
        d.Rect(nameof(PadButton.DUp), 86, 78, 22, 26, 4, "▲", 10);
        d.Rect(nameof(PadButton.DDown), 86, 128, 22, 26, 4, "▼", 10);
        d.Rect(nameof(PadButton.DLeft), 58, 105, 26, 22, 4, "◀", 10);
        d.Rect(nameof(PadButton.DRight), 110, 105, 26, 22, 4, "▶", 10);
        // Aktionstasten
        d.Circle(nameof(PadButton.North), 345, 86, 15, "△", 15, Color.FromArgb(255, 64, 226, 160));
        d.Circle(nameof(PadButton.East), 377, 116, 15, "○", 15, Color.FromArgb(255, 255, 102, 102));
        d.Circle(nameof(PadButton.South), 345, 146, 15, "✕", 15, Color.FromArgb(255, 124, 178, 255));
        d.Circle(nameof(PadButton.West), 313, 116, 15, "□", 15, Color.FromArgb(255, 255, 140, 210));
        // Sticks (Stick bewegen und Stick drücken zeigen dieselbe Stelle)
        d.Circle(nameof(PadButton.LeftStick), 160, 170, 24, "L", 12, alsoKey: nameof(PadButton.L3));
        d.Circle(nameof(PadButton.RightStick), 280, 170, 24, "R", 12, alsoKey: nameof(PadButton.R3));
        d.Label("Create", 112, 88, 10);
        d.Label("Options", 292, 88, 10);
        return d;
    }

    // ------------------------------------------------------------------
    // Wii Remote + Nunchuk
    // ------------------------------------------------------------------

    public static ControllerDiagram WiiRemoteNunchuk()
    {
        var d = new ControllerDiagram(330, 300);
        // Kabel Nunchuk → Wii Remote
        var cable = new PathFigure { StartPoint = new Windows.Foundation.Point(72, 284) };
        cable.Segments.Add(new BezierSegment
        {
            Point1 = new Windows.Foundation.Point(72, 300),
            Point2 = new Windows.Foundation.Point(150, 300),
            Point3 = new Windows.Foundation.Point(210, 252),
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(cable);
        d.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Stroke = d._line, StrokeThickness = 2, Data = geometry });
        // Wii Remote (Schütteln = ganzes Gehäuse)
        d.Body(new Rectangle { RadiusX = 20, RadiusY = 20 }, 40, 8, 64, 276, "Wii Remote schütteln");
        d.Rect("Zeiger (auf den Bildschirm zeigen)", 50, 14, 44, 12, 5, null);
        d.Label("Zeiger", 106, 12, 10);
        d.Rect("Steuerkreuz ↑", 64, 44, 16, 18, 3, null);
        d.Rect("Steuerkreuz ↓", 64, 78, 16, 18, 3, null);
        d.Rect("Steuerkreuz ←", 46, 62, 18, 16, 3, null);
        d.Rect("Steuerkreuz →", 80, 62, 18, 16, 3, null);
        d.Circle("A", 72, 124, 15, "A", 14);
        // B sitzt auf der Unterseite – daneben als Abzug angedeutet
        d.Rect("B", 110, 104, 30, 40, 10, "B", 14, dashed: true);
        d.Label("Unterseite", 106, 146, 10);
        d.Circle("−", 52, 172, 8, "−", 11);
        d.Circle("Home", 72, 172, 10, "⌂", 11);
        d.Circle("+", 92, 172, 8, "+", 11);
        d.Circle("1", 72, 214, 12, "1", 12);
        d.Circle("2", 72, 246, 12, "2", 12);
        // Nunchuk (Schütteln = ganzes Gehäuse)
        d.Body(new Ellipse(), 190, 70, 96, 190, "Nunchuk schütteln");
        d.Circle("Nunchuk-Stick", 238, 118, 24, "", 10);
        d.Rect("Nunchuk C", 292, 92, 26, 20, 9, "C", 12);
        d.Rect("Nunchuk Z", 292, 118, 26, 28, 6, "Z", 12);
        d.Label("Nunchuk", 208, 262, 11);
        return d;
    }

    // ------------------------------------------------------------------
    // GameCube-Controller
    // ------------------------------------------------------------------

    public static ControllerDiagram GameCube()
    {
        var d = new ControllerDiagram(400, 250);
        d.Rect("L", 50, 12, 90, 24, 10, "L");
        d.Rect("R", 260, 12, 90, 24, 10, "R");
        d.Body(new Ellipse(), 28, 96, 124, 150);
        d.Body(new Ellipse(), 248, 96, 124, 150);
        d.Body(new Rectangle { RadiusX = 60, RadiusY = 60 }, 36, 38, 328, 142);
        d.Rect("Z", 276, 42, 60, 13, 6, "Z", 10);
        d.Circle("Control-Stick", 100, 100, 24, "", 10, alsoKey: "Control-Stick langsam");
        d.Circle("Start/Pause", 200, 104, 9, "", 9);
        d.Label("Start", 188, 116, 10);
        d.Circle("A", 300, 108, 21, "A", 16, Color.FromArgb(255, 80, 210, 120));
        d.Circle("B", 262, 140, 12, "B", 12, Color.FromArgb(255, 255, 102, 102));
        d.Rect("X", 330, 84, 18, 32, 9, "X", 11);
        d.Rect("Y", 284, 70, 32, 16, 8, "Y", 11);
        d.Rect("Steuerkreuz ↑", 134, 152, 12, 14, 2, null);
        d.Rect("Steuerkreuz ↓", 134, 178, 12, 14, 2, null);
        d.Rect("Steuerkreuz ←", 120, 166, 14, 12, 2, null);
        d.Rect("Steuerkreuz →", 146, 166, 14, 12, 2, null);
        d.Circle("C-Stick", 258, 184, 17, "C", 12, Color.FromArgb(255, 250, 210, 60));
        return d;
    }

    // ------------------------------------------------------------------
    // Wii U GamePad (dieselben Tasten wie der Wii U Pro Controller)
    // ------------------------------------------------------------------

    public static ControllerDiagram WiiUGamePad()
    {
        var d = new ControllerDiagram(440, 250);
        d.Rect("ZL", 44, 4, 70, 16, 8, "ZL", 10);
        d.Rect("L", 36, 22, 90, 18, 8, "L", 11);
        d.Rect("ZR", 326, 4, 70, 16, 8, "ZR", 10);
        d.Rect("R", 314, 22, 90, 18, 8, "R", 11);
        d.Body(new Rectangle { RadiusX = 34, RadiusY = 34 }, 8, 38, 424, 196);
        d.Rect(null, 110, 58, 220, 140, 6, "Bildschirm (Touch = Maus)", 11);
        d.Circle("Linker Stick", 60, 86, 22, "L", 12, alsoKey: "Linken Stick drücken");
        d.Circle("Rechter Stick", 380, 86, 22, "R", 12, alsoKey: "Rechten Stick drücken");
        d.Rect("Steuerkreuz ↑", 52, 128, 16, 18, 3, null);
        d.Rect("Steuerkreuz ↓", 52, 162, 16, 18, 3, null);
        d.Rect("Steuerkreuz ←", 34, 146, 18, 16, 3, null);
        d.Rect("Steuerkreuz →", 68, 146, 18, 16, 3, null);
        d.Circle("X", 380, 132, 11, "X", 11);
        d.Circle("A", 402, 154, 11, "A", 11);
        d.Circle("B", 380, 176, 11, "B", 11);
        d.Circle("Y", 358, 154, 11, "Y", 11);
        d.Circle("+", 404, 206, 8, "+", 11);
        d.Circle("−", 404, 224, 7, "−", 10);
        d.Circle("Home", 220, 216, 10, "⌂", 11);
        return d;
    }

    // ------------------------------------------------------------------
    // Switch Pro Controller
    // ------------------------------------------------------------------

    public static ControllerDiagram SwitchPro()
    {
        var d = new ControllerDiagram(420, 260);
        d.Rect("ZL", 50, 6, 84, 20, 9, "ZL", 10);
        d.Rect("L", 50, 30, 84, 16, 8, "L", 10);
        d.Rect("ZR", 286, 6, 84, 20, 9, "ZR", 10);
        d.Rect("R", 286, 30, 84, 16, 8, "R", 10);
        d.Body(new Ellipse(), 22, 104, 128, 150);
        d.Body(new Ellipse(), 270, 104, 128, 150);
        d.Body(new Rectangle { RadiusX = 56, RadiusY = 56 }, 24, 48, 372, 150);
        d.Circle("Linker Stick", 108, 100, 24, "L", 12, alsoKey: "Linken Stick drücken");
        d.Rect("Steuerkreuz ↑", 158, 140, 16, 18, 3, null);
        d.Rect("Steuerkreuz ↓", 158, 174, 16, 18, 3, null);
        d.Rect("Steuerkreuz ←", 140, 158, 18, 16, 3, null);
        d.Rect("Steuerkreuz →", 174, 158, 18, 16, 3, null);
        d.Circle("Rechter Stick", 262, 166, 24, "R", 12, alsoKey: "Rechten Stick drücken");
        d.Circle("X", 322, 76, 13, "X", 12);
        d.Circle("A", 348, 100, 13, "A", 12);
        d.Circle("B", 322, 124, 13, "B", 12);
        d.Circle("Y", 296, 100, 13, "Y", 12);
        d.Circle("−", 166, 72, 8, "−", 11);
        d.Circle("+", 254, 72, 8, "+", 11);
        d.Circle("Home", 240, 104, 10, "⌂", 11);
        d.Rect(null, 172, 96, 16, 16, 3, null); // Capture
        return d;
    }

    // ------------------------------------------------------------------
    // Zeichen-Helfer
    // ------------------------------------------------------------------

    private void Body(Shape shape, double x, double y, double w, double h, string? key = null)
    {
        shape.Width = w;
        shape.Height = h;
        shape.Fill = _body;
        shape.Stroke = _line;
        shape.StrokeThickness = 1.5;
        Place(shape, x, y, key);
    }

    private void Rect(string? key, double x, double y, double w, double h, double radius, string? text, double fontSize = 11, bool dashed = false)
    {
        var r = new Rectangle
        {
            Width = w,
            Height = h,
            RadiusX = radius,
            RadiusY = radius,
            Fill = _button,
            Stroke = _line,
            StrokeThickness = 1.5,
        };
        if (dashed)
            r.StrokeDashArray = [3, 2];
        Place(r, x, y, key);
        if (text != null)
            Centered(text, x, y, w, h, fontSize, null);
    }

    private void Circle(string key, double cx, double cy, double radius, string text, double fontSize, Color? textColor = null, string? alsoKey = null)
    {
        var e = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = _button,
            Stroke = _line,
            StrokeThickness = 1.5,
        };
        Place(e, cx - radius, cy - radius, key);
        if (alsoKey != null)
            Register(alsoKey, e);
        if (text.Length > 0)
            Centered(text, cx - radius, cy - radius, radius * 2, radius * 2, fontSize, textColor);
    }

    private void Label(string text, double x, double y, double fontSize)
    {
        var t = new TextBlock { Text = text, FontSize = fontSize, Foreground = _line };
        SetLeft(t, x);
        SetTop(t, y);
        Children.Add(t);
    }

    private void Centered(string text, double x, double y, double w, double h, double fontSize, Color? color)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = color is { } c ? new SolidColorBrush(c) : _text,
            Width = w,
            Height = h,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Padding = new Thickness(0, Math.Max(0, (h - fontSize * 1.33) / 2), 0, 0),
        };
        SetLeft(t, x);
        SetTop(t, y);
        Children.Add(t);
    }

    private void Place(Shape shape, double x, double y, string? key)
    {
        SetLeft(shape, x);
        SetTop(shape, y);
        Children.Add(shape);
        if (key != null)
            Register(key, shape);
    }

    private void Register(string key, Shape shape)
    {
        if (!_parts.TryGetValue(key, out var list))
            _parts[key] = list = [];
        list.Add(shape);
        _original[shape] = (shape.Fill, shape.Stroke, shape.StrokeThickness);
    }
}
