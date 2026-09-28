using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.App.Views;

/// <summary>Einstellungen eines Spiels aus „Alle Spiele“: Name, Cover, Dateipfad, im Hauptmenü ausblenden.</summary>
public sealed partial class GameSettingsPage : Page, IHubPage
{
    private GameEntry? _game;
    private TextBox? _name;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück";

    public GameSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _game = App.Hub.Library.Find(e.Parameter as string ?? "");
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
        Body.Children.Add(Ui.Title("Einstellungen"));
        Body.Children.Add(Ui.Subtle($"{g.Platform.DisplayName()}   ·   {g.GameCode ?? "ohne ID"}"));

        // Name
        Body.Children.Add(Ui.Header("Name im Hauptmenü"));
        _name = new TextBox { Text = g.Title, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left, FontSize = 17 };
        _name.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                SaveName();
                e.Handled = true;
            }
        };
        Body.Children.Add(_name);
        var nameButtons = new List<UIElement> { Ui.Action("Name speichern", "", SaveName, primary: true) };
        if (g.CustomTitle)
            nameButtons.Add(Ui.AsyncAction("Originalnamen verwenden", "", ResetNameAsync));
        Body.Children.Add(Ui.Buttons([.. nameButtons]));

        // Cover
        Body.Children.Add(Ui.Header("Cover"));
        var coverBox = new Border
        {
            // quadratisch wie die Kachel im Hauptmenü
            Width = 256,
            Height = 256,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (g.CoverPath != null && File.Exists(g.CoverPath))
        {
            coverBox.Child = new Image
            {
                Source = new BitmapImage(new Uri(g.CoverPath)) { CreateOptions = BitmapCreateOptions.IgnoreImageCache },
                Stretch = Stretch.UniformToFill,
            };
        }
        else
        {
            coverBox.Child = new TextBlock
            {
                Text = "Kein Cover",
                Foreground = Ui.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        Body.Children.Add(coverBox);
        Body.Children.Add(Ui.Subtle(g.CoverPath ?? "Standard-Kachel (kein Bild)"));
        var coverButtons = new List<UIElement> { Ui.AsyncAction("Bild wählen …", "", PickCoverAsync) };
        if (g.CoverPath != null)
        {
            coverButtons.Add(Ui.AsyncAction("Ausschnitt anpassen …", "", RecropCoverAsync));
            coverButtons.Add(Ui.Action("Bild im Explorer zeigen", "", () => SystemService.ShowInExplorer(g.CoverPath)));
            coverButtons.Add(Ui.Action("Cover entfernen", "", () =>
            {
                App.Hub.Library.ResetCover(g);
                MainWindow.Current.ShowToast("Cover entfernt");
                Build(keepFocus: true);
            }));
        }
        Body.Children.Add(Ui.Buttons([.. coverButtons]));

        // Spieldatei
        Body.Children.Add(Ui.Header("Spieldatei"));
        Body.Children.Add(Ui.Subtle(string.IsNullOrEmpty(g.Path) ? "Keine Datei (eingebautes Spiel)" : g.Path));
        if (!string.IsNullOrEmpty(g.Path))
            Body.Children.Add(Ui.Buttons(Ui.Action("Dateipfad öffnen", "", () => SystemService.ShowInExplorer(g.Path))));

        // Sichtbarkeit
        Body.Children.Add(Ui.Header("Hauptmenü"));
        Body.Children.Add(Ui.Toggle("Im Hauptmenü ausblenden", g.HiddenOnHome, v =>
        {
            g.HiddenOnHome = v;
            App.Hub.Library.Save(g);
            MainWindow.Current.Sounds.Play(UiSound.Toggle);
        }));
        Body.Children.Add(Ui.Subtle("Ausgeblendete Spiele bleiben unter „Alle Spiele“ sichtbar und startbar."));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
        else
            DispatcherQueue.TryEnqueue(() => _name?.Focus(FocusState.Keyboard));
    }

    private void SaveName()
    {
        if (_game == null || _name == null)
            return;
        var name = _name.Text.Trim();
        if (name.Length == 0 || name == _game.Title)
            return;
        _game.Title = name;
        _game.CustomTitle = true;
        App.Hub.Library.Save(_game);
        MainWindow.Current.ShowToast("Name gespeichert ✓");
        Build(keepFocus: true);
    }

    private async Task ResetNameAsync()
    {
        if (_game == null)
            return;
        _game.CustomTitle = false;
        App.Hub.Library.Save(_game);
        await App.Hub.RefreshLibraryAsync(); // holt den Namen aus der Spieldatei zurück
        _game = App.Hub.Library.Find(_game.Id) ?? _game;
        Build(keepFocus: true);
    }

    private async Task PickCoverAsync()
    {
        if (_game == null)
            return;
        var file = await Dialogs.PickFileAsync(".png", ".jpg", ".jpeg", ".webp");
        if (file != null)
            await CropAndSaveAsync(file);
    }

    /// <summary>Quadratischen Ausschnitt neu wählen – vom gemerkten Originalbild, sonst vom aktuellen Cover.</summary>
    private async Task RecropCoverAsync()
    {
        if (_game == null)
            return;
        var source = App.Hub.Library.CoverSourcePath(_game) ?? _game.CoverPath;
        if (source != null && File.Exists(source))
            await CropAndSaveAsync(source);
    }

    private async Task CropAndSaveAsync(string source)
    {
        if (_game == null)
            return;
        string? cropped = null;
        try
        {
            cropped = await CoverCropDialog.ShowAsync(source);
            if (cropped == null)
                return;
            App.Hub.Library.SetCoverSource(_game, source);
            App.Hub.Library.SetCover(_game, cropped);
            MainWindow.Current.ShowToast("Cover gespeichert ✓");
        }
        catch (Exception ex)
        {
            HubLog.Warn("Cover konnte nicht übernommen werden", ex);
            await Dialogs.MessageAsync("Cover", $"Das Bild konnte nicht übernommen werden: {ex.Message}");
        }
        finally
        {
            if (cropped != null)
                File.Delete(cropped);
        }
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action) => false;
}
