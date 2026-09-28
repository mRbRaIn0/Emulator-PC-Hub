using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Input;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Controller-Empfehlung als Chips: ★ empfohlen (Akzent), normal = geht, „!“ = eingeschränkt,
/// ausgegraut + durchgestrichen = geht nicht.
/// </summary>
public static class ControllerAdviceView
{
    /// <summary>Empfehlung für ein Spiel mit dem Emulator, mit dem es gerade starten würde.</summary>
    public static ControllerAdviceResult? For(GameEntry game)
    {
        try
        {
            var backend = App.Hub.AdapterFor(game, App.Hub.Presets.Active(game)).Id;
            return ControllerAdvisor.For(game, backend);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Kurzform für die Info-Zeile des Homescreens.</summary>
    public static void FillCompact(Panel target, ControllerAdviceResult? advice)
    {
        target.Children.Clear();
        if (advice == null)
        {
            target.Children.Add(new TextBlock { Text = "–", FontSize = 18, Foreground = Ui.TextBrush });
            return;
        }
        foreach (var item in advice.Items)
            target.Children.Add(Chip(item, 16));
    }

    /// <summary>Ausführliche Karte mit Begründung je Eingabegerät (Spielseite).</summary>
    public static Border Details(ControllerAdviceResult advice)
    {
        var rows = new List<UIElement>();
        if (advice.Best is { } best)
            rows.Add(Ui.Text($"Empfohlen: {best.Label}" + (advice.Style.Length > 0 ? $"   ·   Spielart: {advice.Style}" : ""), 17, bold: true));
        foreach (var item in advice.Items)
        {
            var grid = new Grid { ColumnSpacing = 16 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var chip = Chip(item, 15);
            chip.VerticalAlignment = VerticalAlignment.Center;
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Children.Add(chip);
            var reason = Ui.Subtle($"{FitText(item.Fit)} – {item.Reason}");
            reason.TextWrapping = TextWrapping.Wrap;
            reason.VerticalAlignment = VerticalAlignment.Center;
            if (item.Fit == InputFit.No)
                reason.Opacity = 0.7;
            Grid.SetColumn(reason, 1);
            grid.Children.Add(reason);
            rows.Add(grid);
        }
        rows.Add(Ui.Subtle("Abgeleitet aus Plattform, Emulator und Spielart – nicht jedes Spiel ist einzeln getestet."));
        return Ui.Card([.. rows]);
    }

    public static string FitText(InputFit fit) => fit switch
    {
        InputFit.Recommended => "Empfohlen",
        InputFit.Works => "Geht",
        InputFit.Limited => "Eingeschränkt",
        _ => "Geht nicht",
    };

    private static Border Chip(InputAdvice item, double size)
    {
        var text = new TextBlock
        {
            Text = (item.Fit == InputFit.Recommended ? "★ " : "") + item.Label + (item.Fit == InputFit.Limited ? " !" : ""),
            FontSize = size,
            FontWeight = item.Fit == InputFit.Recommended ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = item.Fit == InputFit.No ? Ui.SubtleBrush : Ui.TextBrush,
        };
        if (item.Fit == InputFit.No)
            text.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
        var border = new Border
        {
            Child = text,
            Padding = new Thickness(10, 3, 10, 4),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(item.Fit == InputFit.Recommended ? 2 : 1),
            BorderBrush = item.Fit switch
            {
                InputFit.Recommended => Ui.AccentBrush,
                InputFit.Limited => (Brush)Application.Current.Resources["HubWarnBrush"],
                _ => Ui.SubtleBrush,
            },
            Opacity = item.Fit == InputFit.No ? 0.45 : 1,
        };
        ToolTipService.SetToolTip(border, $"{FitText(item.Fit)}: {item.Reason}");
        return border;
    }
}
