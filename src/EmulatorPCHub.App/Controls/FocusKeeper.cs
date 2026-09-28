using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace EmulatorPCHub.App.Controls;

/// <summary>Merkt sich den Fokus beim Neuaufbau einer Seite, damit die Controller-Bedienung nicht „springt“.</summary>
public static class FocusKeeper
{
    public static int Capture(FrameworkElement root)
    {
        if (root.XamlRoot == null)
            return -1;
        var focused = root.XamlRoot is { } xamlRoot ? FocusManager.GetFocusedElement(xamlRoot) as DependencyObject : null;
        if (focused == null)
            return -1;
        return Focusables(root).IndexOf(focused as Control ?? null!);
    }

    public static void Restore(FrameworkElement root, int index)
    {
        if (index < 0)
            return;
        root.DispatcherQueue.TryEnqueue(() =>
        {
            var list = Focusables(root);
            if (list.Count > 0)
                list[Math.Min(index, list.Count - 1)].Focus(FocusState.Keyboard);
        });
    }

    public static List<Control> Focusables(DependencyObject root)
    {
        var result = new List<Control>();
        Walk(root, result);
        return result;
    }

    private static void Walk(DependencyObject d, List<Control> result)
    {
        var count = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(d, i);
            if (child is Control { IsTabStop: true, IsEnabled: true, Visibility: Visibility.Visible } c && c is not ScrollViewer)
            {
                result.Add(c);
                if (c is Button or ToggleSwitch)
                    continue;
            }
            Walk(child, result);
        }
    }
}
