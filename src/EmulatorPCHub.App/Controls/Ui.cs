using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Controls;

/// <summary>Baukasten für einheitliche, controllerfreundliche Oberflächen-Elemente.</summary>
public static class Ui
{
    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    public static Brush TextBrush => Res<Brush>("HubTextBrush");
    public static Brush SubtleBrush => Res<Brush>("HubSubtleBrush");
    public static Brush AccentBrush => Res<Brush>("HubAccentBrush");

    public static TextBlock Title(string text) => new() { Text = text, Style = Res<Style>("HubTitleText") };

    public static TextBlock Header(string text) => new() { Text = text, Style = Res<Style>("HubHeaderText") };

    public static TextBlock Text(string text, double size = 15, bool bold = false) => new()
    {
        Text = text,
        Style = Res<Style>("HubBodyText"),
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
    };

    public static TextBlock Subtle(string text) => new() { Text = text, Style = Res<Style>("HubSubtleText") };

    public static Border Card(params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var c in children)
            panel.Children.Add(c);
        return new Border { Style = Res<Style>("HubCard"), Child = panel, Margin = new Thickness(0, 0, 0, 14) };
    }

    public static Button Action(string text, string? glyph, Action onClick, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (glyph != null)
            content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        content.Children.Add(new TextBlock { Text = text });
        var b = new Button
        {
            Content = content,
            Style = Res<Style>(primary ? "HubPrimaryButton" : "HubButton"),
            Margin = new Thickness(0, 0, 10, 10),
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Button AsyncAction(string text, string? glyph, Func<Task> onClick, bool primary = false)
    {
        Button? b = null;
        b = Action(text, glyph, async () =>
        {
            b!.IsEnabled = false;
            try { await onClick(); }
            finally { b.IsEnabled = true; }
        }, primary);
        return b;
    }

    /// <summary>Auswahl-Karte (Edition/Engine/Preset) mit ●/○ wie im Plan-Entwurf.</summary>
    public static Button Option(OptionItem item, Action<OptionItem> onSelect)
    {
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var radio = new TextBlock
        {
            Text = item.Radio,
            FontSize = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = item.IsSelected ? AccentBrush : SubtleBrush,
        };
        grid.Children.Add(radio);
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(new TextBlock { Text = item.Title, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
        if (!string.IsNullOrEmpty(item.Subtitle))
            texts.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 13, Foreground = SubtleBrush, TextWrapping = TextWrapping.Wrap });
        if (item.Features.Count > 0)
            texts.Children.Add(new TextBlock { Text = item.FeaturesText, FontSize = 12, Foreground = SubtleBrush, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);
        if (!string.IsNullOrEmpty(item.Status))
        {
            var status = new TextBlock
            {
                Text = item.Status,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = item.IsAvailable ? SubtleBrush : Res<Brush>("HubWarnBrush"),
            };
            Grid.SetColumn(status, 2);
            grid.Children.Add(status);
        }
        var button = new Button { Content = grid, Style = Res<Style>("HubOptionButton") };
        if (item.IsSelected)
        {
            button.BorderBrush = AccentBrush;
            button.BorderThickness = new Thickness(2);
        }
        button.Click += (_, _) => onSelect(item);
        return button;
    }

    public static ToggleSwitch Toggle(string header, bool value, Action<bool> onChanged)
    {
        var t = new ToggleSwitch
        {
            Header = header,
            IsOn = value,
            Style = Res<Style>("HubToggle"),
            OnContent = "An",
            OffContent = "Aus",
        };
        t.Toggled += (_, _) => onChanged(t.IsOn);
        return t;
    }

    /// <summary>Statuszeile: Symbol + Text (grün = ok, orange = Hinweis).</summary>
    public static StackPanel Status(string text, bool ok)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = ok ? "✓" : "!",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Foreground = Res<Brush>(ok ? "HubGreenBrush" : "HubWarnBrush"),
                    Width = 16,
                },
                new TextBlock { Text = text, FontSize = 15, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap, MaxWidth = 620 },
            },
        };
    }

    public static WrapPanelLike Buttons(params UIElement[] buttons)
    {
        var p = new WrapPanelLike();
        foreach (var b in buttons)
            p.Children.Add(b);
        return p;
    }
}

/// <summary>Einfaches umbrechendes Panel für Button-Reihen.</summary>
public sealed class WrapPanelLike : Panel
{
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        double x = 0, y = 0, rowH = 0, maxW = 0;
        foreach (var child in Children)
        {
            child.Measure(new Windows.Foundation.Size(availableSize.Width, double.PositiveInfinity));
            var s = child.DesiredSize;
            if (x > 0 && x + s.Width > availableSize.Width)
            {
                y += rowH;
                x = 0;
                rowH = 0;
            }
            x += s.Width;
            rowH = Math.Max(rowH, s.Height);
            maxW = Math.Max(maxW, x);
        }
        return new Windows.Foundation.Size(double.IsInfinity(availableSize.Width) ? maxW : Math.Min(maxW, availableSize.Width), y + rowH);
    }

    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        double x = 0, y = 0, rowH = 0;
        foreach (var child in Children)
        {
            var s = child.DesiredSize;
            if (x > 0 && x + s.Width > finalSize.Width)
            {
                y += rowH;
                x = 0;
                rowH = 0;
            }
            child.Arrange(new Windows.Foundation.Rect(x, y, s.Width, s.Height));
            x += s.Width;
            rowH = Math.Max(rowH, s.Height);
        }
        return finalSize;
    }
}
