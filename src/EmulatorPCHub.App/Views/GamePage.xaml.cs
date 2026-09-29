using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>Spielseite für normale Spiele: Presets, Emulator, Spielzeit, Favorit, Start.</summary>
public sealed partial class GamePage : Page, IHubPage
{
    private GameEntry? _game;
    private string? _selectedPreset;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Favorit   Ⓨ Starten";

    public GamePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _game = App.Hub.Library.Find(e.Parameter as string ?? "");
        _selectedPreset = _game?.ActivePresetId;
        // Frame.Navigated (→ OnShown) kommt vor OnNavigatedTo – daher hier mit Spiel neu aufbauen
        Build();
    }

    public void OnShown()
    {
        if (_game != null)
            Build();
    }

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        if (_game == null)
        {
            Body.Children.Add(Ui.Title("Spiel nicht gefunden"));
            return;
        }
        var g = _game;
        var tile = new GameTileViewModel(g, "", "");
        MainWindow.Current.SetBackdrop(tile.Accent, tile.AccentDark, tile.BackgroundPath);

        var header = new Grid { ColumnSpacing = 32 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(new GameTileView(tile, 220));
        var info = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(Ui.Title(g.DisplayTitle));
        info.Children.Add(Ui.Subtle($"{g.Platform.DisplayName()}   ·   {g.GameCode ?? "ohne ID"}   ·   {Format.Size(g.FileSize)}"));
        info.Children.Add(Ui.Text($"Spielzeit: {Format.PlayTime(g.PlayTimeSeconds)}", 17, bold: true));
        info.Children.Add(Ui.Subtle(Format.LastPlayed(g.LastPlayed)));
        var start = Ui.Action("Starten", "", () => _ = LaunchAsync(), primary: true);
        start.Margin = new Thickness(0, 10, 0, 0);
        info.Children.Add(Ui.Buttons(start,
            Ui.Action(g.IsFavorite ? "★ Favorit" : "☆ Als Favorit", null, ToggleFavorite)));
        Grid.SetColumn(info, 1);
        header.Children.Add(info);
        Body.Children.Add(header);

        if (ControllerAdviceView.For(g) is { } advice)
        {
            Body.Children.Add(Ui.Header("Controller-Empfehlung"));
            Body.Children.Add(ControllerAdviceView.Details(advice));
        }

        Body.Children.Add(Ui.Header("Preset"));
        foreach (var p in App.Hub.Presets.ForGame(g))
        {
            var item = new OptionItem
            {
                Id = p.Id,
                Title = p.Name,
                Subtitle = string.IsNullOrEmpty(p.Arguments) ? p.Description : $"{p.Description} · Argumente: {p.Arguments}",
                IsSelected = p.Id == (_selectedPreset ?? "standard"),
            };
            Body.Children.Add(Ui.Option(item, sel =>
            {
                _selectedPreset = sel.Id;
                g.ActivePresetId = sel.Id;
                App.Hub.Library.Save(g);
                MainWindow.Current.Sounds.Play(UiSound.Toggle);
                Build(keepFocus: true);
            }));
        }
        Body.Children.Add(Ui.Buttons(Ui.AsyncAction("Eigenes Preset (Startparameter) …", "", AddPresetAsync)));

        // Statistik dieses Spiels (aktives Profil)
        var st = App.Hub.Stats.ForGame(g, App.Hub.Profiles.Active.Id);
        Body.Children.Add(Ui.Header($"Statistik ({App.Hub.Profiles.Active.Name})"));
        Body.Children.Add(Ui.Card(
            Ui.Subtle($"{st.Sessions} Sessions   ·   {st.Starts} Starts" + (st.FailedStarts > 0 ? $" ({st.FailedStarts} fehlgeschlagen)" : "") +
                      $"   ·   Ø {Format.PlayTime((long)st.Average.TotalSeconds)}   ·   längste {Format.PlayTime((long)st.Longest.TotalSeconds)}" +
                      $"   ·   letzter Start {(st.LastStart?.LocalDateTime.ToString("dd.MM.yyyy HH:mm") ?? "–")}"),
            Ui.Buttons(
                Ui.Action("Spielstände", "", () => MainWindow.Current.Navigate(typeof(SavesPage), g.Id)),
                Ui.Action("Gesamtstatistik", "", () => MainWindow.Current.Navigate(typeof(StatisticsPage), App.Hub.Profiles.Active.Id)))));

        Body.Children.Add(Ui.Header("Emulator"));
        var adapter = App.Hub.AdapterFor(g, null);
        var inst = adapter.DetectInstallation();
        Body.Children.Add(Ui.Card(
            Ui.Status(inst.IsInstalled ? $"{adapter.DisplayName} {inst.Version}" : $"{adapter.DisplayName} nicht installiert", inst.IsInstalled),
            Ui.Subtle($"Datei: {g.Path}")));
        foreach (var problem in inst.Problems)
            Body.Children.Add(Ui.Status(problem, false));
        Body.Children.Add(Ui.Buttons(
            Ui.Action("Ordner öffnen", "", () => SystemService.OpenFolder(g.Path)),
            Ui.AsyncAction("Cover wählen", "\uEB9F", ChooseCoverAsync),
            Ui.Action("Controller-Profil", "\uE7FC", () => MainWindow.Current.Navigate(typeof(ControllerProfilePage), g.Id))));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
        else
            DispatcherQueue.TryEnqueue(() => start.Focus(FocusState.Keyboard));
    }

    private Task LaunchAsync()
    {
        var preset = App.Hub.Presets.Active(_game!, _selectedPreset);
        return MainWindow.Current.LaunchAsync(_game!, preset);
    }

    private void ToggleFavorite()
    {
        App.Hub.Library.ToggleFavorite(_game!);
        MainWindow.Current.Sounds.Play(UiSound.Favorite);
        Build(keepFocus: true);
    }

    private async Task AddPresetAsync()
    {
        var name = new TextBox { Header = "Name", Text = "Eigenes Preset" };
        var args = new TextBox { Header = "Zusätzliche Startparameter", PlaceholderText = "z. B. -C Dolphin.Core.GFXBackend=Vulkan" };
        var panel = new StackPanel { Spacing = 10, MinWidth = 420, Children = { name, args } };
        var result = await Dialogs.ShowAsync("Preset anlegen", panel, "Speichern", "Abbrechen");
        if (result != ContentDialogResult.Primary)
            return;
        var preset = new GamePreset
        {
            Id = "custom-" + Guid.NewGuid().ToString("N")[..6],
            Name = name.Text,
            Game = _game!.Title,
            Platform = _game.Platform.ShortName(),
            Backend = _game.EmulatorId,
            Arguments = args.Text,
            Kind = PresetKind.Custom,
            Description = "Eigenes Preset",
        };
        App.Hub.Presets.SaveCustom(_game, preset);
        Build();
    }

    private async Task ChooseCoverAsync()
    {
        var file = await Dialogs.PickFileAsync(".png", ".jpg", ".jpeg");
        if (file == null)
            return;
        var dir = App.Hub.Library.ArtworkDir(_game!);
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "cover.*"))
            File.Delete(old);
        File.Copy(file, Path.Combine(dir, "cover" + Path.GetExtension(file).ToLowerInvariant()), overwrite: true);
        App.Hub.Library.ResolveArtwork(_game!);
        App.Hub.Library.Save(_game!);
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action)
    {
        switch (action)
        {
            case NavAction.Y:
                _ = LaunchAsync();
                return true;
            case NavAction.X:
                ToggleFavorite();
                return true;
            default:
                return false;
        }
    }
}
