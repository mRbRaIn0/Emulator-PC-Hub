using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.App.Controls;

/// <summary>Rundes Abzeichen für ein Mii: Lieblingsfarbe + Anfangsbuchstabe (der Hub zeichnet keine Mii-Gesichter).</summary>
public static class MiiBadge
{
    public static Border Create(HubMii mii, double size = 64)
    {
        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = AvatarView.Brush(mii.ColorHex),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderThickness = new Thickness(Math.Max(2, size / 24)),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = mii.Name.Length > 0 ? mii.Name[..1].ToUpperInvariant() : "?",
                FontSize = size * 0.45,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(mii.Info?.FavoriteColor is 2 or 10 ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}
