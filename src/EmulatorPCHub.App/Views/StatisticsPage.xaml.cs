using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Library;
using EmulatorPCHub.UI;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Spielestatistik: Spielzeit, Sessions, Starts, Durchschnitt, längste Session, letzter Start,
/// meistgespielte Woche/Monat, Spielzeit nach Plattform/Profil/Preset (z. B. Vanilla, Retro Rewind, CTGP Deluxe)
/// und Verlauf als Diagramm – für alle Profile oder ein einzelnes (L/R wechselt).
/// </summary>
public sealed partial class StatisticsPage : Page, IHubPage
{
    private string? _profileId;
    private bool _weeks;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   L/R Profil wechseln";

    public StatisticsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _profileId = e.Parameter as string;
        Build();
    }

    public void OnShown() => Build();

    private string ProfileName(string? id) =>
        id == null ? "Alle Profile" : App.Hub.Profiles.Find(id)?.Name ?? (id.Length == 0 ? "ohne Profil (älter)" : "gelöschtes Profil");

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        var s = App.Hub.Stats.Overview(_profileId);
        Body.Children.Add(Ui.Title("Statistik"));

        var filter = new List<UIElement>();
        foreach (var id in new string?[] { null }.Concat(App.Hub.Profiles.All.Select(p => p.Id)))
        {
            var pid = id;
            var b = Ui.Action(ProfileName(pid), null, () =>
            {
                _profileId = pid;
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                Build();
            });
            if (pid == _profileId)
            {
                b.BorderBrush = Ui.AccentBrush;
                b.BorderThickness = new Thickness(2);
            }
            filter.Add(b);
        }
        Body.Children.Add(Ui.Buttons([.. filter]));

        if (s.Sessions == 0 && s.Starts == 0)
        {
            Body.Children.Add(Ui.Subtle("Noch keine Spielzeit erfasst. Starte ein Spiel über den Hub – danach erscheint hier die Auswertung."));
            return;
        }

        // Kennzahlen
        var tiles = new WrapPanelLike();
        void Tile(string label, string value, string? sub = null)
        {
            var p = new StackPanel { Spacing = 2, Width = 250 };
            p.Children.Add(Ui.Subtle(label));
            p.Children.Add(new TextBlock { Text = value, FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = Ui.TextBrush });
            if (sub != null)
                p.Children.Add(new TextBlock { Text = sub, FontSize = 13, Foreground = Ui.SubtleBrush, TextTrimming = TextTrimming.CharacterEllipsis });
            var card = Ui.Card(p);
            card.Margin = new Thickness(0, 0, 12, 12);
            tiles.Children.Add(card);
        }
        Tile("Gesamtspielzeit", Duration(s.Total));
        Tile("Sessions", s.Sessions.ToString());
        Tile("Starts", s.Starts.ToString());
        Tile("Ø Session", Duration(s.Average));
        Tile("Längste Session", Duration(s.Longest), s.LongestGame);
        Tile("Letzter Start", s.LastStart?.LocalDateTime.ToString("dd.MM.yyyy HH:mm") ?? "–");
        Tile("Meistgespielte Woche", s.BestWeek?.Key ?? "–", s.BestWeek != null ? Duration(s.BestWeek.Total) : null);
        Tile("Meistgespielter Monat", s.BestMonth?.Key ?? "–", s.BestMonth != null ? Duration(s.BestMonth.Total) : null);
        Body.Children.Add(tiles);

        // Verlauf
        Body.Children.Add(Ui.Header("Verlauf"));
        var toggle = Ui.Action(_weeks ? "Letzte 12 Wochen  ⇄  30 Tage" : "Letzte 30 Tage  ⇄  12 Wochen", "", () =>
        {
            _weeks = !_weeks;
            Build(keepFocus: true);
        });
        Body.Children.Add(toggle);
        Body.Children.Add(Ui.Card(Chart(_weeks ? s.Last12Weeks : s.Last30Days, _weeks ? 1 : 5)));

        // Aufteilungen
        Body.Children.Add(Ui.Header("Spielzeit nach Plattform"));
        Body.Children.Add(Ui.Card(Bars(s.ByPlatform.Select(x => (x.Key, x.Total, x.Sessions)))));
        if (_profileId == null)
        {
            Body.Children.Add(Ui.Header("Spielzeit nach Profil"));
            Body.Children.Add(Ui.Card(Bars(s.ByProfile.Select(x => (ProfileName(x.Key), x.Total, x.Sessions)))));
        }
        Body.Children.Add(Ui.Header("Spielzeit nach Preset / Mod"));
        Body.Children.Add(Ui.Card(Bars(s.ByPreset.Select(x => (x.Key, x.Total, x.Sessions)))));

        // Spiele
        Body.Children.Add(Ui.Header("Spiele"));
        foreach (var g in s.Games)
            Body.Children.Add(GameRow(g));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private static string Duration(TimeSpan t) => t.TotalMinutes < 1
        ? $"{(int)t.TotalSeconds} s"
        : Format.PlayTime((long)t.TotalSeconds);

    /// <summary>Säulendiagramm (Minuten pro Tag bzw. Woche).</summary>
    private static UIElement Chart(IReadOnlyList<StatBucket> buckets, int labelEvery)
    {
        const double height = 180;
        var max = Math.Max(1, buckets.Max(b => b.Total.TotalMinutes));
        var grid = new Grid { Height = height + 46, ColumnSpacing = 4 };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height + 20) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < buckets.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var b = buckets[i];
            var minutes = b.Total.TotalMinutes;
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            if (minutes > 0)
                column.Children.Add(new TextBlock
                {
                    Text = minutes >= 60 ? $"{Math.Floor(minutes / 6) / 10:0.#}h" : $"{Math.Max(1, Math.Floor(minutes)):0}m",
                    FontSize = 11,
                    Foreground = Ui.SubtleBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            column.Children.Add(new Border
            {
                Height = Math.Max(minutes > 0 ? 3 : 1, minutes / max * height),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Background = minutes > 0 ? Ui.AccentBrush : Ui.SubtleBrush,
                Opacity = minutes > 0 ? 1 : 0.3,
            });
            Grid.SetColumn(column, i);
            grid.Children.Add(column);
            if (i % labelEvery == 0 || i == buckets.Count - 1)
            {
                var label = new TextBlock { Text = b.Label, FontSize = 11, Foreground = Ui.SubtleBrush, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(label, i);
                Grid.SetRow(label, 1);
                grid.Children.Add(label);
            }
        }
        return grid;
    }

    /// <summary>Waagerechte Balken mit Anteil an der Gesamtspielzeit.</summary>
    private static UIElement Bars(IEnumerable<(string Label, TimeSpan Total, int Sessions)> rows)
    {
        var list = rows.ToList();
        var panel = new StackPanel { Spacing = 8 };
        if (list.Count == 0)
        {
            panel.Children.Add(Ui.Subtle("–"));
            return panel;
        }
        var max = Math.Max(1, list.Max(r => r.Total.TotalSeconds));
        var sum = Math.Max(1, list.Sum(r => r.Total.TotalSeconds));
        foreach (var (label, total, sessions) in list)
        {
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            grid.Children.Add(new TextBlock { Text = label, Foreground = Ui.TextBrush, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis });
            var track = new Grid();
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(total.TotalSeconds / max, GridUnitType.Star) });
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - total.TotalSeconds / max + 0.0001, GridUnitType.Star) });
            track.Children.Add(new Border { Height = 14, CornerRadius = new CornerRadius(7), Background = Ui.AccentBrush, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(track, 1);
            grid.Children.Add(track);
            var value = new TextBlock
            {
                Text = $"{Duration(total)}  ·  {total.TotalSeconds / sum:P0}  ·  {sessions}×",
                Foreground = Ui.SubtleBrush,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
            panel.Children.Add(grid);
        }
        return panel;
    }

    private UIElement GameRow(GameStats g)
    {
        var text = $"{g.Title}   ·   {Duration(g.Total)}   ·   {g.Sessions} Sessions   ·   {g.Starts} Starts" +
                   (g.FailedStarts > 0 ? $" ({g.FailedStarts} fehlgeschlagen)" : "") +
                   $"   ·   Ø {Duration(g.Average)}   ·   längste {Duration(g.Longest)}" +
                   (g.LastStart != null ? $"   ·   zuletzt {g.LastStart.Value.LocalDateTime:dd.MM.yyyy}" : "");
        var b = Ui.Action(text, null, () =>
        {
            if (App.Hub.Library.Find(g.GameId) is { } game)
                MainWindow.Current.Navigate(typeof(GamePage), game.Id);
        });
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        return b;
    }

    public bool HandleNav(NavAction action)
    {
        if (action is not (NavAction.L or NavAction.R))
            return false;
        var ids = new List<string?> { null };
        ids.AddRange(App.Hub.Profiles.All.Select(p => p.Id));
        var idx = ids.IndexOf(_profileId);
        _profileId = ids[(idx + (action == NavAction.R ? 1 : -1) + ids.Count) % ids.Count];
        MainWindow.Current.Sounds.Play(UiSound.Toggle);
        Build();
        return true;
    }
}
