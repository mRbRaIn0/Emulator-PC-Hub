using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.UI;
using EmulatorPCHub.UI.ViewModels;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Komponenten (Plan Abschnitte 15 + 16): Status aller externen Komponenten, Versionshinweise,
/// Installation/Update aus einer vom Nutzer selbst heruntergeladenen Datei. Der Hub lädt nichts herunter.
/// </summary>
public sealed partial class DownloadsPage : Page, IHubPage
{
    private static ComponentsViewModel? _vm;
    private readonly DispatcherQueueTimer _refresh;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Nach Updates suchen";

    public DownloadsPage()
    {
        InitializeComponent();
        _vm ??= new ComponentsViewModel(App.Hub);
        _refresh = DispatcherQueue.CreateTimer();
        _refresh.Interval = TimeSpan.FromMilliseconds(600);
        _refresh.Tick += (_, _) =>
        {
            if (_vm.Items.Any(i => i.IsBusy) || _vm.IsChecking)
                Build(keepFocus: true);
        };
        Unloaded += (_, _) => _refresh.Stop();
    }

    public void OnShown()
    {
        _vm!.Reload();
        Build();
        _refresh.Start();
        _ = ScanLocalAsync();
        if (_vm.Items.All(i => string.IsNullOrEmpty(i.LatestVersion)))
            _ = CheckAsync();
    }

    private async Task ScanLocalAsync()
    {
        await _vm!.ScanLocalPackagesAsync();
        Build(keepFocus: true);
    }

    private async Task CheckAsync()
    {
        var task = _vm!.CheckUpdatesAsync();
        Build(keepFocus: true);
        await task;
        Build(keepFocus: true);
    }

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Komponenten"));
        Body.Children.Add(Ui.Subtle(_vm!.Summary + (_vm.IsChecking ? "   ·   Suche nach Updates …" : "")));
        Body.Children.Add(Ui.Buttons(
            Ui.AsyncAction("Nach Updates suchen", "", CheckAsync),
            Ui.Action("integrations-Ordner öffnen", "", () => SystemService.OpenFolder(App.Hub.Paths.Integrations))));
        Body.Children.Add(Ui.Subtle("Der Hub lädt nichts herunter. Lade Emulatoren, Tools und Mods selbst von der offiziellen " +
                                    "Webseite des jeweiligen Projekts und installiere sie hier „Aus Datei“ (oder lege das Archiv in " +
                                    "integrations\\_downloads ab). Spiele, Keys und Firmware stellst du aus deinen eigenen Dumps bereit."));

        foreach (var item in _vm.Items)
            Body.Children.Add(BuildItem(item));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private UIElement BuildItem(ComponentItemViewModel item)
    {
        var grid = new Grid { ColumnSpacing = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = AvatarView.Brush(item.AccentColor),
            Child = new TextBlock
            {
                Text = item.Glyph,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            VerticalAlignment = VerticalAlignment.Top,
        };
        grid.Children.Add(badge);

        var info = new StackPanel { Spacing = 4 };
        info.Children.Add(new TextBlock
        {
            Text = $"{item.Name}   ·   {item.Category}",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ui.TextBrush,
        });
        info.Children.Add(Ui.Subtle(item.Description));
        var versions = item.StatusText;
        if (!string.IsNullOrEmpty(item.InstalledVersion))
            versions += $"   ·   installiert: {item.InstalledVersion}";
        if (!string.IsNullOrEmpty(item.LatestVersion))
            versions += $"   ·   aktuell: {item.LatestVersion}";
        info.Children.Add(Ui.Text(versions, 15, bold: true));
        if (!string.IsNullOrEmpty(item.Problems))
            foreach (var p in item.Problems.Split('\n'))
                info.Children.Add(Ui.Status(p, false));
        if (item.IsBusy)
        {
            info.Children.Add(new ProgressBar
            {
                IsIndeterminate = item.Progress <= 0 || item.Progress >= 100,
                Value = item.Progress,
                Margin = new Thickness(0, 6, 0, 2),
            });
            info.Children.Add(Ui.Subtle(item.ProgressText));
        }
        else if (!string.IsNullOrEmpty(item.ProgressText))
        {
            info.Children.Add(Ui.Subtle(item.ProgressText));
        }

        var buttons = new List<UIElement>();
        var act = Ui.AsyncAction(item.ActionText, "\uE8E5", () => InstallFromPickedFileAsync(item));
        act.IsEnabled = item.CanAct;
        buttons.Add(act);
        if (!string.IsNullOrEmpty(item.InstallPath))
            buttons.Add(Ui.Action("Ordner öffnen", "", () => SystemService.OpenFolder(item.InstallPath)));
        if (!string.IsNullOrEmpty(item.Changelog))
            buttons.Add(Ui.AsyncAction("Changelog", "", () => Dialogs.MessageAsync($"{item.Name} {item.LatestVersion}", item.Changelog)));
        buttons.Add(Ui.Action("Offizielle Webseite", "\uE774", () => SystemService.OpenUrl(item.Homepage)));

        // Selbst abgelegte Pakete (z. B. wenn der offizielle Server nicht erreichbar ist)
        if (_vm!.LocalPackages.TryGetValue(item.Id, out var packages))
        {
            foreach (var pkg in packages)
            {
                var name = System.IO.Path.GetFileName(pkg.Path);
                if (pkg.Valid && item.IsInstalled && !string.IsNullOrEmpty(item.InstalledVersion)
                    && name.Contains(item.InstalledVersion, StringComparison.OrdinalIgnoreCase))
                    continue; // genau diese Version ist schon installiert
                if (pkg.Valid)
                {
                    info.Children.Add(Ui.Status($"Lokales Paket gefunden: {name}", true));
                    buttons.Insert(0, Ui.AsyncAction($"{name} installieren", "\uE896", () => InstallLocalAsync(item, pkg.Path)));
                }
                else
                {
                    info.Children.Add(Ui.Status($"„{name}“ enthält keine {Updates.ComponentInstaller.ExpectedExecutable(item.Id)} – " +
                                                "kein Windows-Paket (z. B. Quellcode oder Webseite). Die Datei kann gelöscht werden.", false));
                }
            }
        }
        info.Children.Add(Ui.Buttons([.. buttons]));
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        return Ui.Card(grid);
    }

    private async Task InstallFromPickedFileAsync(ComponentItemViewModel item)
    {
        var file = item.Id is Core.Models.ComponentIds.RetroRewind or Core.Models.ComponentIds.CtgpDeluxe
            ? await Dialogs.PickFileAsync(".zip")
            : await Dialogs.PickFileAsync(".zip", ".7z", ".exe");
        if (file != null)
            await InstallLocalAsync(item, file);
    }

    private async Task InstallLocalAsync(ComponentItemViewModel item, string file)
    {
        if (!await Dialogs.ConfirmAsync($"{item.Name} installieren",
                $"„{System.IO.Path.GetFileName(file)}“ wird geprüft und installiert.\n" +
                "Vorhandene Einstellungen bleiben erhalten (vorher Backup).", "Installieren"))
            return;
        var task = _vm!.InstallFromFileAsync(item, file);
        Build(keepFocus: true);
        var error = await task;
        Build(keepFocus: true);
        if (error != null)
            await Dialogs.MessageAsync(item.Name, error);
        else
            MainWindow.Current.ShowToast($"{item.Name} installiert ✓");
    }


    public bool HandleNav(NavAction action)
    {
        if (action == NavAction.X)
        {
            _ = CheckAsync();
            return true;
        }
        return false;
    }
}
