using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Save Manager: Spielstände pro Spiel und Profil – Sichern, Wiederherstellen, Exportieren, Importieren,
/// Duplizieren (für ein anderes Profil) und automatische Snapshots vor/nach dem Spielen.
/// Ohne Parameter: Übersicht aller Spiele; mit Spiel-ID: Details dieses Spiels.
/// </summary>
public sealed partial class SavesPage : Page, IHubPage
{
    private GameEntry? _game;
    private UserProfile _profile = App.Hub.Profiles.Active;

    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück";

    public SavesPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _game = e.Parameter is string id ? App.Hub.Library.Find(id) : null;
        _profile = App.Hub.Profiles.Active;
        Build();
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        if (_game == null)
            BuildOverview();
        else
            BuildGame(_game);
        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private UIElement ProfileSwitcher() => Ui.Buttons(
        Ui.AsyncAction($"Profil: {_profile.Name}", "", async () =>
        {
            var all = App.Hub.Profiles.All.ToList();
            var idx = await Dialogs.ChooseAsync("Spielstände von …", all.Select(p => p.Name).ToList());
            if (idx >= 0)
            {
                _profile = all[idx];
                Build();
            }
        }),
        Ui.Toggle("Automatische Snapshots vor/nach dem Spielen", App.Hub.Config.Current.Launch.AutoSaveSnapshots,
            v => App.Hub.Config.Update(c => c.Launch.AutoSaveSnapshots = v)));

    private void BuildOverview()
    {
        Body.Children.Add(Ui.Title("Spielstände"));
        Body.Children.Add(Ui.Subtle("Spielstände pro Spiel und Profil sichern und wiederherstellen. Profile selbst verwaltest du unter „Profile“."));
        Body.Children.Add(ProfileSwitcher());

        var rows = App.Hub.Library.Games
            .Where(g => !g.IsPlaceholder || g.GameCode != null)
            .Select(g => (Game: g, Has: SafeHas(g), Snapshots: App.Hub.Saves.Snapshots(g, _profile.Id).Count))
            .OrderByDescending(r => r.Has || r.Snapshots > 0)
            .ThenByDescending(r => r.Game.LastPlayed ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.Game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var (g, has, snaps) in rows)
        {
            var status = has ? "Spielstand vorhanden" : SaveManager.Unsupported(g) ?? "Noch kein Spielstand";
            var b = Ui.Action($"{g.Title}   ·   {g.Platform.ShortName()}   ·   {status}" + (snaps > 0 ? $"   ·   {snaps} Sicherung(en)" : ""),
                has ? "" : "", () => MainWindow.Current.Navigate(typeof(SavesPage), g.Id));
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            Body.Children.Add(b);
        }
    }

    private bool SafeHas(GameEntry g)
    {
        try { return App.Hub.Saves.HasSaves(g, _profile); }
        catch (Exception) { return false; }
    }

    private void BuildGame(GameEntry g)
    {
        Body.Children.Add(Ui.Title("Spielstände – " + g.Title));
        Body.Children.Add(Ui.Subtle($"{g.Platform.DisplayName()}   ·   {g.GameCode ?? "ohne ID"}"));
        Body.Children.Add(ProfileSwitcher());

        var locations = App.Hub.Saves.Locations(g, _profile);
        Body.Children.Add(Ui.Header("Speicherorte"));
        if (locations.Count == 0)
            Body.Children.Add(Ui.Status(SaveManager.Unsupported(g) ?? "Für dieses Spiel ist kein Speicherort bekannt (Emulator einmal starten).", false));
        foreach (var loc in locations)
        {
            Body.Children.Add(Ui.Card(
                Ui.Status($"{loc.Description}: {(loc.Exists ? "vorhanden" : "leer")}", loc.Exists),
                Ui.Subtle(loc.Path),
                Ui.Buttons(Ui.Action("Ordner öffnen", "", () => SystemService.OpenFolder(Directory.Exists(loc.Path) ? loc.Path : Path.GetDirectoryName(loc.Path))))));
        }
        if (locations.Any(l => l.Shared && l.Key is "wii" or "gc"))
            Body.Children.Add(Ui.Subtle("Dolphin kennt keine Benutzer: Wii-/GameCube-Spielstände gelten für alle Profile. " +
                                        "Sicherungen werden trotzdem pro Profil abgelegt, sodass jedes Profil seinen Stand wiederherstellen kann."));

        Body.Children.Add(Ui.Buttons(
            Ui.AsyncAction("Jetzt sichern", "", () => BackupAsync(g), primary: true),
            Ui.AsyncAction("Sicherung importieren …", "", () => ImportAsync(g))));

        var snapshots = App.Hub.Saves.Snapshots(g, _profile.Id);
        Body.Children.Add(Ui.Header($"Sicherungen von {_profile.Name} ({snapshots.Count})"));
        if (snapshots.Count == 0)
            Body.Children.Add(Ui.Subtle("Noch keine Sicherungen. Automatische Snapshots entstehen vor und nach dem Spielen, sobald ein Spielstand existiert."));
        foreach (var s in snapshots)
            Body.Children.Add(BuildSnapshot(g, s));
    }

    private UIElement BuildSnapshot(GameEntry g, SaveSnapshot s)
    {
        var m = s.Manifest;
        return Ui.Card(
            Ui.Text($"{m.Created.LocalDateTime:dd.MM.yyyy HH:mm}   ·   {(string.IsNullOrWhiteSpace(m.Label) ? "Sicherung" : m.Label)}" +
                    (m.Auto ? "   ·   automatisch" : ""), 17, bold: true),
            Ui.Subtle($"{Format.Size(s.Size)}   ·   Profil {m.ProfileName}   ·   {string.Join(", ", m.Locations)}"),
            Ui.Buttons(
                Ui.AsyncAction("Wiederherstellen", "", () => RestoreAsync(g, s)),
                Ui.AsyncAction("Exportieren …", "", async () =>
                {
                    var folder = await Dialogs.PickFolderAsync();
                    if (folder != null)
                        MainWindow.Current.ShowToast("Exportiert: " + Path.GetFileName(App.Hub.Saves.Export(s, folder)));
                }),
                Ui.AsyncAction("Für Profil duplizieren …", "", () => DuplicateAsync(g, s)),
                Ui.AsyncAction("Löschen", "", async () =>
                {
                    if (await Dialogs.ConfirmAsync("Sicherung löschen", "Diese Sicherung endgültig löschen?", "Löschen"))
                    {
                        App.Hub.Saves.Delete(s);
                        Build(keepFocus: true);
                    }
                })));
    }

    private async Task BackupAsync(GameEntry g)
    {
        var label = await Dialogs.InputAsync("Spielstand sichern", "Bezeichnung (optional)", "Manuelle Sicherung", 60);
        if (label == null)
            return;
        await Run(() => App.Hub.Saves.CreateSnapshot(g, _profile, label, auto: false) != null
            ? "Spielstand gesichert ✓"
            : "Kein Spielstand vorhanden");
    }

    private async Task RestoreAsync(GameEntry g, SaveSnapshot s)
    {
        if (App.Hub.Pipeline.IsRunning)
        {
            await Dialogs.MessageAsync("Wiederherstellen", "Bitte zuerst das laufende Spiel beenden.");
            return;
        }
        if (!await Dialogs.ConfirmAsync("Spielstand wiederherstellen",
                $"Den Spielstand vom {s.Manifest.Created.LocalDateTime:dd.MM.yyyy HH:mm} für „{_profile.Name}“ wiederherstellen?\n\n" +
                "Der aktuelle Stand wird vorher automatisch gesichert.", "Wiederherstellen"))
            return;
        await Run(() =>
        {
            App.Hub.Saves.Restore(s, g, _profile);
            return "Spielstand wiederhergestellt ✓";
        });
    }

    private async Task DuplicateAsync(GameEntry g, SaveSnapshot s)
    {
        var others = App.Hub.Profiles.All.Where(p => p.Id != _profile.Id).ToList();
        if (others.Count == 0)
        {
            await Dialogs.MessageAsync("Duplizieren", "Es gibt nur ein Profil. Lege unter „Profile“ ein weiteres an (z. B. Spieler 2).");
            return;
        }
        var idx = await Dialogs.ChooseAsync("Sicherung kopieren für …", others.Select(p => p.Name).ToList(),
            "Die Sicherung erscheint danach beim anderen Profil und kann dort wiederhergestellt werden.");
        if (idx < 0)
            return;
        var target = others[idx];
        await Run(() =>
        {
            App.Hub.Saves.Duplicate(s, g, target);
            return $"Für „{target.Name}“ kopiert ✓";
        });
    }

    private async Task ImportAsync(GameEntry g)
    {
        var file = await Dialogs.PickFileAsync(".zip");
        if (file == null)
            return;
        await Run(() =>
        {
            App.Hub.Saves.Import(file, g, _profile);
            return "Sicherung importiert ✓ – jetzt „Wiederherstellen“ wählen";
        });
    }

    /// <summary>Dateiarbeit im Hintergrund, Meldung danach im UI-Thread.</summary>
    private async Task Run(Func<string> work)
    {
        try
        {
            MainWindow.Current.ShowToast(await Task.Run(work));
        }
        catch (Exception ex)
        {
            HubLog.Warn("Save Manager", ex);
            await Dialogs.MessageAsync("Spielstände", "Fehlgeschlagen: " + ex.Message);
        }
        Build(keepFocus: true);
    }

    public bool HandleNav(NavAction action) => false;
}
