using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using EmulatorPCHub.Controllers.Wii;
using Windows.UI;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Live-Anzeige des IR-Sensortests: links das Kamerabild der Wii Remote (die Punkte der Sensorleiste),
/// rechts Zeiger, Tasten und Neigung. Wird vom Aufrufer ~30× pro Sekunde mit <see cref="Update"/> gefüttert.
/// </summary>
public sealed class SensorView
{
    private const double W = 384, H = 288;
    private readonly Canvas _camera = new() { Width = W, Height = H };
    private readonly Canvas _screen = new() { Width = 256, Height = 144 };
    private readonly Ellipse[] _dots = new Ellipse[4];
    private readonly Ellipse _pointer;
    private readonly TextBlock _verdict = Ui.Text("", 17, bold: true);
    private readonly TextBlock _detail = Ui.Subtle("");
    private readonly TextBlock _buttons = Ui.Text("", 15);
    private readonly TextBlock _tilt = Ui.Subtle("");
    private readonly TextBlock _state = Ui.Subtle("");

    public UIElement Root { get; }

    public SensorView()
    {
        var dark = new SolidColorBrush(Color.FromArgb(255, 0x0B, 0x0E, 0x14));
        for (var i = 0; i < 4; i++)
        {
            _dots[i] = new Ellipse { Width = 14, Height = 14, Fill = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xF1, 0xC4)), Visibility = Visibility.Collapsed };
            _camera.Children.Add(_dots[i]);
        }
        _pointer = new Ellipse { Width = 16, Height = 16, Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 3, Visibility = Visibility.Collapsed };
        _screen.Children.Add(_pointer);

        var cameraBox = new Border { Background = dark, CornerRadius = new CornerRadius(8), Child = _camera, HorizontalAlignment = HorizontalAlignment.Left };
        var screenBox = new Border
        {
            Background = dark, CornerRadius = new CornerRadius(6), Child = _screen, HorizontalAlignment = HorizontalAlignment.Left,
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0x44, 0x4A, 0x55)), BorderThickness = new Thickness(2),
        };

        var left = new StackPanel { Spacing = 6 };
        left.Children.Add(Ui.Subtle("Kamerabild der Wii Remote – die Sensorleiste erscheint als 2 helle Punkte"));
        left.Children.Add(cameraBox);

        var right = new StackPanel { Spacing = 8, MinWidth = 280 };
        right.Children.Add(_verdict);
        right.Children.Add(_detail);
        right.Children.Add(Ui.Subtle("Zeiger auf dem Bildschirm"));
        right.Children.Add(screenBox);
        right.Children.Add(_buttons);
        right.Children.Add(_tilt);
        right.Children.Add(_state);

        var grid = new Grid { ColumnSpacing = 24 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        Root = grid;
    }

    public void Update(WiiSensorSample? s, string state)
    {
        _state.Text = state;
        if (s == null || DateTime.UtcNow - s.At > TimeSpan.FromSeconds(2))
        {
            foreach (var d in _dots)
                d.Visibility = Visibility.Collapsed;
            _pointer.Visibility = Visibility.Collapsed;
            _verdict.Text = "Warte auf Daten der Wii Remote …";
            _detail.Text = "Wii Remote eingeschaltet und verbunden? Eine Taste drücken.";
            _buttons.Text = "";
            _tilt.Text = "";
            return;
        }

        for (var i = 0; i < 4; i++)
        {
            if (i < s.Dots.Count)
            {
                var dot = s.Dots[i];
                var size = 8 + dot.Size * 2.0;
                _dots[i].Width = _dots[i].Height = size;
                // Gespiegelt, damit sich die Punkte wie in einem Spiegel mitbewegen
                Canvas.SetLeft(_dots[i], (1 - dot.X / 1023.0) * W - size / 2);
                Canvas.SetTop(_dots[i], dot.Y / 767.0 * H - size / 2);
                _dots[i].Visibility = Visibility.Visible;
            }
            else
            {
                _dots[i].Visibility = Visibility.Collapsed;
            }
        }

        var pointer = WiiRemoteProtocol.Pointer(s.Dots);
        if (pointer is { } p)
        {
            Canvas.SetLeft(_pointer, p.X * _screen.Width - 8);
            Canvas.SetTop(_pointer, p.Y * _screen.Height - 8);
            _pointer.Visibility = Visibility.Visible;
        }
        else
        {
            _pointer.Visibility = Visibility.Collapsed;
        }

        (_verdict.Text, _detail.Text) = s.Dots.Count switch
        {
            0 => ("✗ Keine Sensorpunkte sichtbar",
                "Wii Remote auf die Leiste richten (1–3 m Abstand). Bleibt es leer: Leiste hat keinen Strom (USB/Netz) oder steht verdeckt."),
            1 => ("! Nur 1 Punkt sichtbar",
                "Ein Ende der Leiste ist verdeckt oder außerhalb des Blickfelds – etwas weiter weg gehen bzw. gerader zielen."),
            2 => ("✓ Sensorleiste funktioniert",
                $"2 Punkte, Abstand {Distance(s.Dots[0], s.Dots[1]):0} px (größer = näher dran). Zeiger folgt der Wii Remote."),
            _ => ($"! {s.Dots.Count} Punkte – Störlicht",
                "Neben der Leiste sieht die Kamera weitere IR-Quellen (Sonne, Kerzen, Halogenlampen, Spiegelungen). Diese abschirmen."),
        };

        _buttons.Text = s.Buttons.Count > 0 ? "Gedrückt: " + string.Join("  ", s.Buttons) : "Tasten: keine gedrückt (zum Testen drücken)";
        var (ax, ay, az) = s.Accel;
        // Rohwerte ~512 = 0 g, ~100 Schritte pro g
        var roll = Math.Atan2(ax - 512, az - 512) * 180 / Math.PI;
        var pitch = Math.Atan2(ay - 512, az - 512) * 180 / Math.PI;
        _tilt.Text = $"Neigung: seitlich {roll:+0;-0;0}° · vor/zurück {pitch:+0;-0;0}°";
    }

    private static double Distance(IrDot a, IrDot b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
