using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using EmulatorPCHub.Core;

namespace EmulatorPCHub.App.Services;

/// <summary>Dialoge und Datei-Auswahl (controllerfreundlich: A = bestätigen, B = abbrechen).</summary>
public static class Dialogs
{
    public static XamlRoot? Root { get; set; }
    public static IntPtr Hwnd { get; set; }
    public static ContentDialog? Current { get; private set; }

    public static async Task<bool> ConfirmAsync(string title, string text, string yes = "OK", string no = "Abbrechen")
    {
        var result = await ShowAsync(title, text, yes, no);
        return result == ContentDialogResult.Primary;
    }

    public static Task MessageAsync(string title, string text) => ShowAsync(title, text, null, "OK");

    public static async Task<ContentDialogResult> ShowAsync(string title, object content, string? primary, string? close,
        string? secondary = null)
    {
        if (Root == null)
            return ContentDialogResult.None;
        title = Loc.T(title);
        primary = primary == null ? null : Loc.T(primary);
        close = close == null ? null : Loc.T(close);
        secondary = secondary == null ? null : Loc.T(secondary);
        Current?.Hide();
        var theme = (Root.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default;
        var dialog = new ContentDialog
        {
            XamlRoot = Root,
            RequestedTheme = theme,
            Title = new TextBlock
            {
                Text = title,
                FontSize = 28,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrush("HubTextBrush", theme),
            },
            Content = content is string s
                ? new ScrollViewer
                {
                    Content = new TextBlock { Text = Loc.T(s), TextWrapping = TextWrapping.Wrap, FontSize = 17, Foreground = ThemeBrush("HubSubtleBrush", theme) },
                    MaxHeight = 460,
                }
                : content,
            PrimaryButtonText = primary ?? "",
            SecondaryButtonText = secondary ?? "",
            CloseButtonText = close ?? "",
            // Kein DefaultButton: der erzwingt sonst den Windows-Akzentstil statt des Hub-Looks.
            DefaultButton = ContentDialogButton.None,
            PrimaryButtonStyle = (Style)Application.Current.Resources["HubDialogPrimaryButton"],
            SecondaryButtonStyle = (Style)Application.Current.Resources["HubDialogButton"],
            CloseButtonStyle = (Style)Application.Current.Resources["HubDialogButton"],
            Background = ThemeBrush("HubSurfaceBrush", theme),
            BorderBrush = ThemeBrush("HubDividerBrush", theme),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
        };
        // Reine Text-Dialoge haben nichts Fokussierbares: Fokus auf die Haupttaste (für A am Controller).
        if (content is string)
        {
            var focusName = primary != null ? "PrimaryButton" : "CloseButton";
            dialog.Opened += (_, _) => (FindChild(dialog, focusName) as Control)?.Focus(FocusState.Keyboard);
        }
        Current = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            if (Current == dialog)
                Current = null;
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush? ThemeBrush(string key, ElementTheme theme)
    {
        var dicts = Application.Current.Resources.ThemeDictionaries;
        var dictKey = theme == ElementTheme.Light ? "Light" : "Default";
        return dicts.TryGetValue(dictKey, out var d) && d is ResourceDictionary rd && rd.TryGetValue(key, out var b)
            ? b as Microsoft.UI.Xaml.Media.Brush
            : null;
    }

    private static DependencyObject? FindChild(DependencyObject parent, string name)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name)
                return child;
            if (FindChild(child, name) is { } found)
                return found;
        }
        return null;
    }

    public static async Task<string?> PickFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        foreach (var ext in extensions.Length > 0 ? extensions : ["*"])
            picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>Auswahl aus einer Liste (controllerfreundlich: eine Schaltfläche pro Eintrag). -1 = abgebrochen.</summary>
    public static async Task<int> ChooseAsync(string title, IReadOnlyList<string> options, string? text = null)
    {
        var chosen = -1;
        var panel = new StackPanel { Spacing = 6, MinWidth = 380 };
        if (text != null)
            panel.Children.Add(new TextBlock { Text = Loc.T(text), TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) });
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var b = new Button
            {
                Content = Loc.T(options[i]),
                Style = (Style)Application.Current.Resources["HubOptionButton"],
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontSize = 16,
                Margin = new Thickness(0),
            };
            b.Click += (_, _) =>
            {
                chosen = index;
                Current?.Hide();
            };
            panel.Children.Add(b);
        }
        await ShowAsync(title, new ScrollViewer { Content = panel, MaxHeight = 460 }, null, "Abbrechen");
        return chosen;
    }

    /// <summary>Texteingabe; null = abgebrochen.</summary>
    public static async Task<string?> InputAsync(string title, string header, string value = "", int maxLength = 0)
    {
        var box = new TextBox { Header = Loc.T(header), Text = value, MinWidth = 360, MaxLength = maxLength };
        return await ShowAsync(title, box, "OK", "Abbrechen") == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    public static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, Hwnd);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
