using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using EmulatorPCHub.Core.Input;
using Windows.UI;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Zeigt, was am Controller für Spieler 1–4 leuchtet – so, wie der Hub es über SDL setzt:
/// PlayStation-Lichtleiste Blau/Rot/Grün/Pink (+ weiße Spieler-LEDs beim DualSense), Switch grüne LED n,
/// Wii Remote / Wii U Pro blaue LED n. Xbox hat keine Spieler-LED.
/// </summary>
public static class PlayerLight
{
    private static readonly (string Name, Color Color)[] LightBar =
    [
        ("Blau", Color.FromArgb(255, 0x2F, 0x6B, 0xFF)),
        ("Rot", Color.FromArgb(255, 0xFF, 0x3B, 0x30)),
        ("Grün", Color.FromArgb(255, 0x34, 0xC7, 0x59)),
        ("Pink", Color.FromArgb(255, 0xFF, 0x4F, 0xD8)),
    ];

    private static readonly Color WiiBlue = Color.FromArgb(255, 0x3D, 0x9B, 0xFF);
    private static readonly Color SwitchGreen = Color.FromArgb(255, 0x4C, 0xE0, 0x5A);
    private static readonly Color Off = Color.FromArgb(255, 0x55, 0x5A, 0x63);

    /// <summary>DualSense-Spieler-LEDs unter dem Touchpad (SDL: 1 = Mitte, 2 = innen, 3 = außen + Mitte, 4 = vier).</summary>
    private static readonly int[] DualSensePattern = [0x04, 0x0A, 0x15, 0x1B];

    public static UIElement For(ControllerKind kind, int player)
    {
        if (player is < 1 or > 4)
            return new StackPanel();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        switch (kind)
        {
            case ControllerKind.PlayStation5:
            case ControllerKind.PlayStation4:
            {
                var (name, color) = LightBar[player - 1];
                row.Children.Add(new Rectangle { Width = 34, Height = 10, RadiusX = 5, RadiusY = 5, Fill = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center });
                if (kind == ControllerKind.PlayStation5)
                    row.Children.Add(Leds(5, DualSensePattern[player - 1], Colors.White, small: true));
                row.Children.Add(Ui.Subtle($"Lichtleiste {name}" + (kind == ControllerKind.PlayStation5 ? $" · {player} Spieler-LED{(player == 1 ? "" : "s")}" : "")));
                break;
            }
            case ControllerKind.SwitchPro:
            case ControllerKind.JoyCon:
                row.Children.Add(Leds(4, 1 << (player - 1), SwitchGreen));
                row.Children.Add(Ui.Subtle($"LED {player} leuchtet grün"));
                break;
            case ControllerKind.WiiRemote:
            case ControllerKind.WiiUPro:
                row.Children.Add(Leds(4, 1 << (player - 1), WiiBlue));
                row.Children.Add(Ui.Subtle($"LED {player} leuchtet blau"));
                break;
            case ControllerKind.Xbox:
                row.Children.Add(Ui.Subtle("Xbox-Logo leuchtet weiß (keine Spieler-LED)"));
                break;
            default:
                row.Children.Add(Ui.Subtle("keine Spieler-LED"));
                break;
        }
        return row;
    }

    /// <summary>Wii-Remote-LEDs nach Maske (Bit 0 = LED 1) – z. B. aus dem Status-Report gelesen.</summary>
    public static UIElement WiiLeds(int mask, string? text = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        row.Children.Add(Leds(4, mask, WiiBlue));
        if (text != null)
            row.Children.Add(Ui.Subtle(text));
        return row;
    }

    private static StackPanel Leds(int count, int mask, Color on, bool small = false)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = small ? 3 : 5, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < count; i++)
        {
            var lit = (mask & (1 << i)) != 0;
            panel.Children.Add(new Rectangle
            {
                Width = small ? 6 : 10,
                Height = small ? 6 : 10,
                RadiusX = small ? 3 : 2,
                RadiusY = small ? 3 : 2,
                Fill = new SolidColorBrush(lit ? on : Off),
                Opacity = lit ? 1 : 0.6,
            });
        }
        return panel;
    }
}
