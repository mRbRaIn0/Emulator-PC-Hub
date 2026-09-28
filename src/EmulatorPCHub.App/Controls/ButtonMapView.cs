using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Input;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.App.Controls;

/// <summary>
/// Belegungsansicht: Controller-Grafik(en) links, Tabelle „Taste → Konsolen-Taste → Funktion im Spiel“ rechts.
/// Die fokussierte Zeile leuchtet in der Grafik auf; A/Klick öffnet die Umbelegung (Taste wählen/tauschen)
/// und die Funktion im Spiel. Umbelegungen landen im Controller-Profil und werden beim Start in den Emulator geschrieben.
/// </summary>
public static class ButtonMapView
{
    /// <param name="game">Spiel (null = Standardprofil ohne Funktionen).</param>
    /// <param name="profileKey">Controller-Profil, in dem Umbelegungen gespeichert werden (Spiel-ID oder „default“).</param>
    /// <param name="pad">Emulierter Controller im Spiel.</param>
    /// <param name="source">Gerät des Spielers (für die Tastennamen); PS5, wenn unbekannt.</param>
    /// <param name="realWiimote">Echte Wii Remote wird durchgereicht (keine Umbelegung).</param>
    public static UIElement Build(GameEntry? game, string profileKey, EmulatedPad pad, ControllerKind source, bool realWiimote, Action rebuild)
    {
        var store = App.Hub.Input.GameControls;
        var diagrams = new StackPanel { Spacing = 18 };
        ControllerDiagram? ps = null;
        ControllerDiagram? consoleDiagram = null;
        if (!realWiimote)
        {
            ps = ControllerDiagram.DualSense();
            diagrams.Children.Add(Caption(source is ControllerKind.PlayStation5 or ControllerKind.PlayStation4 or ControllerKind.Keyboard
                ? "Dein Controller (PS5)"
                : $"Dein Controller ({source.DisplayName()}) – im PS5-Schema dargestellt"));
            diagrams.Children.Add(ps);
        }
        // Konsolen-Controller im Spiel (Tasten sind nach den Zielnamen der Belegung benannt)
        (ControllerDiagram diagram, string caption)? console = pad switch
        {
            EmulatedPad.Wiimote => (ControllerDiagram.WiiRemoteNunchuk(), realWiimote ? "Deine Wii Remote + Nunchuk" : "Im Spiel: Wii Remote + Nunchuk"),
            EmulatedPad.GameCube => (ControllerDiagram.GameCube(), "Im Spiel: GameCube-Controller"),
            EmulatedPad.WiiUPro => (ControllerDiagram.WiiUGamePad(), "Im Spiel: Wii U Pro Controller – dieselben Tasten wie am GamePad"),
            EmulatedPad.WiiUGamePad => (ControllerDiagram.WiiUGamePad(), "Im Spiel: Wii U GamePad"),
            EmulatedPad.SwitchPro or EmulatedPad.SwitchJoyConPair => (ControllerDiagram.SwitchPro(), "Im Spiel: Switch Pro Controller"),
            _ => null,
        };
        if (console is { } c)
        {
            consoleDiagram = c.diagram;
            diagrams.Children.Add(Caption(c.caption));
            diagrams.Children.Add(consoleDiagram);
        }

        var nintendo = NintendoLayout(source);
        var profiles = App.Hub.Input.Profiles;
        var rows = ButtonMap.For(pad, nintendo, realWiimote ? null : profiles.For(profileKey).Remap);
        var ctx = new EditContext(game, profileKey, pad, source, nintendo, realWiimote, rows, store, rebuild);

        var table = new StackPanel { Spacing = 0 };
        table.Children.Add(HeaderRow(realWiimote ? "Wii-Taste" : SourceHeader(source),
            $"Im Spiel ({ButtonMap.PadTitle(pad)})", game != null ? "Funktion in diesem Spiel" : null));
        foreach (var row in rows)
            table.Children.Add(Row(ctx, row, ps, consoleDiagram));

        var unused = ButtonMap.Unused(rows);
        if (unused.Count > 0 && !realWiimote)
            table.Children.Add(Note("Im Spiel nicht belegt: " + string.Join(" · ", unused.Select(b => ButtonMap.Label(b, source))), 14, 10));
        table.Children.Add(Note(realWiimote
            ? "Echte Wii Remote: Die Tasten sind fest. Mit A trägst du ein, was sie im Spiel machen."
            : "Mit A auf einer Zeile legst du eine andere Taste fest (belegte Tasten werden getauscht)" +
              (game != null ? " und trägst ein, was sie im Spiel macht." : ".") +
              (store.HasSuggestions(game ?? new GameEntry { Id = "", Title = "" }, pad) ? " Kursiv = Vorschlag des Spiels." : "") +
              (source == ControllerKind.Keyboard ? " Hinweis: Umbelegungen gelten für Controller, nicht für die Tastatur." : ""), 13, 6));
        if (rows.Any(r => r.Changed) && !realWiimote)
        {
            table.Children.Add(Ui.Buttons(Ui.Action("Alle Tasten zurücksetzen", "", () =>
            {
                var profile = profiles.GetOrCreate(profileKey);
                ButtonMap.Reset(profile.Remap, pad);
                profiles.Save();
                MainWindow.Current.ShowToast("Standardbelegung wiederhergestellt");
                rebuild();
            })));
        }

        var layout = new Grid { ColumnSpacing = 28 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.Children.Add(diagrams);
        Grid.SetColumn(table, 1);
        layout.Children.Add(table);
        return layout;
    }

    private sealed record EditContext(GameEntry? Game, string ProfileKey, EmulatedPad Pad, ControllerKind Source, bool Nintendo,
        bool RealWiimote, IReadOnlyList<ButtonRow> Rows, GameControlsStore Store, Action Rebuild);

    private static bool NintendoLayout(ControllerKind k) => k is ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiUPro;

    private static TextBlock Caption(string text) => new() { Text = text, Foreground = Ui.SubtleBrush, FontSize = 14 };

    private static TextBlock Note(string text, double size, double top) => new()
    {
        Text = text,
        Foreground = Ui.SubtleBrush,
        FontSize = size,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(4, top, 0, 0),
    };

    private static Grid Columns(UIElement a, UIElement b, UIElement? c)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = c == null ? new GridLength(1, GridUnitType.Star) : new GridLength(220) });
        if (c != null)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(a);
        Grid.SetColumn((FrameworkElement)b, 1);
        g.Children.Add(b);
        if (c != null)
        {
            Grid.SetColumn((FrameworkElement)c, 2);
            g.Children.Add(c);
        }
        return g;
    }

    private static string SourceHeader(ControllerKind k) => k switch
    {
        ControllerKind.Xbox => "Xbox-Taste",
        ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiUPro => "Deine Taste",
        _ => "PS5-Taste",
    };

    private static UIElement HeaderRow(string a, string b, string? c)
    {
        TextBlock H(string t) => new() { Text = t, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Ui.SubtleBrush, TextWrapping = TextWrapping.Wrap };
        var g = Columns(H(a), H(b), c == null ? null : H(c));
        g.Margin = new Thickness(16, 0, 16, 6);
        return g;
    }

    private static string SourceText(EditContext ctx, ButtonRow row) =>
        ctx.RealWiimote ? row.Target
        : row.Source is { } s ? ButtonMap.Label(s, ctx.Source)
        : "— nicht belegt";

    private static Button Row(EditContext ctx, ButtonRow row, ControllerDiagram? ps, ControllerDiagram? consoleDiagram)
    {
        TextBlock? function = null;
        if (ctx.Game != null)
        {
            var (text, suggestion) = ctx.Store.Get(ctx.Game, ctx.Pad, row.Target);
            function = new TextBlock
            {
                Text = text ?? "—",
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = suggestion ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
                Foreground = text == null ? Ui.SubtleBrush : Ui.TextBrush,
            };
        }
        var changed = row.Changed && !ctx.RealWiimote;
        var sourceBlock = new TextBlock
        {
            Text = SourceText(ctx, row) + (changed ? "  ●" : ""),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = changed ? Ui.AccentBrush : row.Source == null ? Ui.SubtleBrush : Ui.TextBrush,
        };
        var content = Columns(
            sourceBlock,
            new TextBlock { Text = "→ " + row.Target, FontSize = 15, TextWrapping = TextWrapping.Wrap, Foreground = Ui.TextBrush },
            function);
        var button = new Button
        {
            Content = content,
            Style = (Style)Application.Current.Resources["HubOptionButton"],
            Padding = new Thickness(16, 9, 16, 9),
            Margin = new Thickness(0, 0, 0, 4),
        };

        void Show()
        {
            ps?.Highlight(row.Source?.ToString());
            consoleDiagram?.Highlight(row.Target);
        }
        button.GotFocus += (_, _) => Show();
        button.PointerEntered += (_, _) => Show();
        if (ctx.Game != null || !ctx.RealWiimote)
            button.Click += async (_, _) => await EditAsync(ctx, row);
        return button;
    }

    /// <summary>Dialog: Gamepad-Taste für eine Konsolen-Taste wählen (Tausch bei Belegung) + Funktion im Spiel.</summary>
    private static async Task EditAsync(EditContext ctx, ButtonRow row)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 560 };
        var canRemap = !ctx.RealWiimote && !row.Fixed;
        PadButton? selected = row.Source;

        if (!ctx.RealWiimote)
        {
            panel.Children.Add(Ui.Text($"Welche {SourceHeader(ctx.Source)} soll „{row.Target}“ auslösen?", 16, bold: true));
            if (!canRemap)
            {
                panel.Children.Add(Ui.Subtle("Sticks sind fest belegt und lassen sich nicht auf Tasten legen."));
            }
            else
            {
                var chips = new List<(Button button, PadButton? value)>();
                void Mark()
                {
                    foreach (var (b, v) in chips)
                    {
                        b.BorderBrush = v == selected ? Ui.AccentBrush : null;
                        b.BorderThickness = new Thickness(v == selected ? 3 : 0);
                    }
                }
                Button Chip(PadButton? value)
                {
                    var holder = value == null ? null : ctx.Rows.FirstOrDefault(r => r.Source == value && r.Target != row.Target);
                    var label = value is { } v ? ButtonMap.Label(v, ctx.Source) : "Keine (nicht belegen)";
                    if (holder != null)
                        label += $"  ·  jetzt {holder.Target}";
                    var b = Ui.Action(label, null, () =>
                    {
                        selected = value;
                        Mark();
                    });
                    b.Margin = new Thickness(0, 0, 8, 8);
                    chips.Add((b, value));
                    return b;
                }
                var chipPanel = Ui.Buttons([.. ButtonMap.Assignable.Select(v => (UIElement)Chip(v)), Chip(null)]);
                panel.Children.Add(chipPanel);
                Mark();
                panel.Children.Add(Ui.Subtle("Ist die gewählte Taste schon belegt, werden die beiden Tasten getauscht."));
            }
        }

        TextBox? box = null;
        string? current = null;
        if (ctx.Game != null)
        {
            (current, _) = ctx.Store.Get(ctx.Game, ctx.Pad, row.Target);
            box = new TextBox
            {
                Header = "Funktion in diesem Spiel",
                Text = current ?? "",
                PlaceholderText = "z. B. Springen, Angreifen, Item benutzen … (leer = nicht genutzt)",
                FontSize = 16,
            };
            panel.Children.Add(box);
        }

        var result = await Dialogs.ShowAsync($"{SourceText(ctx, row)}  →  {row.Target}",
            new ScrollViewer { Content = panel, MaxHeight = 560 }, "Speichern", "Abbrechen", "Zurücksetzen");
        var profiles = App.Hub.Input.Profiles;
        if (result == ContentDialogResult.Primary)
        {
            if (canRemap && selected != row.Source)
            {
                var profile = profiles.GetOrCreate(ctx.ProfileKey);
                ButtonMap.Assign(profile.Remap, ctx.Pad, ctx.Nintendo, row.Target, selected);
                profiles.Save();
                MainWindow.Current.ShowToast("Belegung gespeichert – gilt ab dem nächsten Spielstart");
            }
            if (ctx.Game != null && box != null && box.Text.Trim() != (current ?? ""))
                ctx.Store.Set(ctx.Game, ctx.Pad, row.Target, box.Text);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            // Taste auf die Standardbelegung zurück (ein getauschter Partner bekommt seine Taste zurück) + Funktion auf Vorschlag
            if (canRemap && row.Changed)
            {
                var profile = profiles.GetOrCreate(ctx.ProfileKey);
                ButtonMap.Assign(profile.Remap, ctx.Pad, ctx.Nintendo, row.Target, row.DefaultSource);
                profiles.Save();
            }
            if (ctx.Game != null)
                ctx.Store.Reset(ctx.Game, ctx.Pad, row.Target);
        }
        else
        {
            return;
        }
        ctx.Rebuild();
    }
}
