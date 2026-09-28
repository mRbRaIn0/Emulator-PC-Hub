using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EmulatorPCHub.App.Controls;
using EmulatorPCHub.App.Services;
using EmulatorPCHub.Controllers;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.UI;

namespace EmulatorPCHub.App.Views;

/// <summary>
/// Profile = Benutzer (z. B. Spieler 1, Gast, Spieler 2) mit Avatar/Mii, bevorzugtem Controller, Favoriten und Statistik
/// sowie der Zuordnung zu Emulator-Benutzern (Cemu-Konto, Eden-Benutzer) für getrennte Spielstände.
/// Die Spielstände selbst verwaltet der Save Manager.
/// </summary>
public sealed partial class ProfilesPage : Page, IHubPage
{
    public string Hints => "Ⓐ Auswählen   Ⓑ Zurück";

    public ProfilesPage()
    {
        InitializeComponent();
    }

    public void OnShown() => Build();

    private void Build(bool keepFocus = false)
    {
        var focus = keepFocus ? FocusKeeper.Capture(this) : -1;
        Body.Children.Clear();
        Body.Children.Add(Ui.Title("Profile"));
        Body.Children.Add(Ui.Subtle("Jedes Profil hat eigene Favoriten, Statistik und – über Cemu-Konto bzw. Eden-Benutzer – eigene Spielstände."));
        Body.Children.Add(Ui.Buttons(
            Ui.AsyncAction("Profil hinzufügen", "", AddAsync, primary: true),
            Ui.Action("Mii Manager", "", () => MainWindow.Current.Navigate(typeof(MiiPage))),
            Ui.Action("Spielstände", "", () => MainWindow.Current.Navigate(typeof(SavesPage))),
            Ui.Action("Statistik", "", () => MainWindow.Current.Navigate(typeof(StatisticsPage)))));

        var active = App.Hub.Profiles.Active;
        foreach (var p in App.Hub.Profiles.All.OrderByDescending(p => p.Id == active.Id))
            Body.Children.Add(BuildProfile(p, p.Id == active.Id));

        if (keepFocus)
            FocusKeeper.Restore(this, focus);
    }

    private UIElement BuildProfile(UserProfile p, bool isActive)
    {
        var grid = new Grid { ColumnSpacing = 22 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var mii = App.Hub.Miis.Find(p.MiiId);
        UIElement picture = mii != null
            ? MiiBadge.Create(mii, 110)
            : new AvatarView { Width = 110, Height = 110, Spec = p.Avatar, VerticalAlignment = VerticalAlignment.Top };
        grid.Children.Add(picture);

        var info = new StackPanel { Spacing = 4 };
        info.Children.Add(Ui.Text(p.Name + (isActive ? "   ✓ aktiv" : ""), 22, bold: true));
        var stats = App.Hub.Stats.Overview(p.Id);
        info.Children.Add(Ui.Subtle($"Spielzeit {Format.PlayTime((long)stats.Total.TotalSeconds)}   ·   {stats.Sessions} Sessions   ·   " +
                                    $"{App.Hub.Library.FavoriteCount(p.Id)} Favoriten   ·   {Format.LastPlayed(stats.LastStart)}"));
        info.Children.Add(Ui.Subtle("Mii: " + (mii != null ? $"{mii.Name} ({EmulatorPCHub.Core.Mii.MiiCodec.PlatformName(mii.Format)})" : "keins (eigener Avatar)")));
        var controller = p.PreferredController == null ? null
            : App.Hub.Input.Controllers.Devices.FirstOrDefault(d => d.Key == p.PreferredController);
        info.Children.Add(Ui.Subtle("Controller: " + (p.PreferredController == null ? "automatisch"
            : controller != null ? $"{controller.Name} (wird Spieler 1)" : "festgelegt (gerade nicht verbunden)")));
        info.Children.Add(Ui.Subtle($"Wii U (Cemu-Konto): {App.Hub.Saves.CemuAccountFor(p)}   ·   " +
                                    $"Switch (Eden-Benutzer): {App.Hub.Saves.EdenUserFor(p)?.Name ?? "–"}" +
                                    (string.IsNullOrWhiteSpace(p.EdenUser) ? " (Standard)" : "")));

        var buttons = new List<UIElement>();
        if (!isActive)
            buttons.Add(Ui.Action("Aktivieren", "", () =>
            {
                App.Hub.Profiles.SetActive(p);
                MainWindow.Current.ShowToast($"Profil „{p.Name}“ aktiv");
                Build();
            }, primary: true));
        buttons.Add(Ui.AsyncAction("Umbenennen", "", async () =>
        {
            var name = await Dialogs.InputAsync("Profil umbenennen", "Name", p.Name, 24);
            if (!string.IsNullOrWhiteSpace(name))
            {
                p.Name = name;
                App.Hub.Profiles.Update(p);
                Build(keepFocus: true);
            }
        }));
        buttons.Add(Ui.AsyncAction("Mii wählen", "", () => ChooseMiiAsync(p)));
        buttons.Add(Ui.Action("Avatar bearbeiten", "", () =>
        {
            App.Hub.Profiles.SetActive(p);
            MainWindow.Current.Navigate(typeof(SettingsPage), "profile");
        }));
        buttons.Add(Ui.AsyncAction("Controller", "", () => ChooseControllerAsync(p)));
        buttons.Add(Ui.AsyncAction("Cemu-Konto", "", () => ChooseCemuAsync(p)));
        buttons.Add(Ui.AsyncAction("Eden-Benutzer", "", () => ChooseEdenAsync(p)));
        buttons.Add(Ui.Action("Statistik", "", () => MainWindow.Current.Navigate(typeof(StatisticsPage), p.Id)));
        if (App.Hub.Profiles.All.Count > 1)
            buttons.Add(Ui.AsyncAction("Löschen", "", () => RemoveAsync(p)));
        info.Children.Add(Ui.Buttons([.. buttons]));

        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        var card = Ui.Card(grid);
        if (isActive)
        {
            card.BorderBrush = Ui.AccentBrush;
            card.BorderThickness = new Thickness(2);
        }
        return card;
    }

    private async Task AddAsync()
    {
        var name = await Dialogs.InputAsync("Neues Profil", "Name (z. B. Gast oder Spieler 2)", "", 24);
        if (string.IsNullOrWhiteSpace(name))
            return;
        App.Hub.Profiles.Add(name);
        Build();
    }

    private async Task ChooseMiiAsync(UserProfile p)
    {
        var miis = App.Hub.Miis.All.ToList();
        var options = new List<string> { "Kein Mii (eigenen Avatar verwenden)" };
        options.AddRange(miis.Select(m => $"{m.Name}   ({EmulatorPCHub.Core.Mii.MiiCodec.PlatformName(m.Format)})"));
        var idx = await Dialogs.ChooseAsync("Mii für " + p.Name, options,
            miis.Count == 0 ? "Noch keine Miis – im Mii Manager importieren oder erstellen." : null);
        if (idx < 0)
            return;
        p.MiiId = idx == 0 ? null : miis[idx - 1].Id;
        if (idx > 0)
            p.Avatar.Background = miis[idx - 1].ColorHex; // Lieblingsfarbe auch für den Avatar
        App.Hub.Profiles.Update(p);
        Build(keepFocus: true);
    }

    private async Task ChooseControllerAsync(UserProfile p)
    {
        var devices = App.Hub.Input.Controllers.Devices.ToList();
        var options = new List<string> { "Automatisch (keine Festlegung)" };
        options.AddRange(devices.Select(d => $"{d.Name}   ({d.Kind.DisplayName()})"));
        var idx = await Dialogs.ChooseAsync("Controller für " + p.Name, options,
            "Der gewählte Controller wird beim Wechsel auf dieses Profil automatisch Spieler 1.");
        if (idx < 0)
            return;
        p.PreferredController = idx == 0 ? null : devices[idx - 1].Key;
        App.Hub.Profiles.Update(p);
        Build(keepFocus: true);
    }

    private async Task ChooseCemuAsync(UserProfile p)
    {
        var accounts = App.Hub.Miis.CemuAccountsList();
        if (accounts.Count == 0)
        {
            await Dialogs.MessageAsync("Cemu-Konto", "Cemu hat noch keine Konten – Cemu einmal starten. Weitere Konten legst du in Cemu unter " +
                                                     "Optionen → Allgemeine Einstellungen → Konto an.");
            return;
        }
        var idx = await Dialogs.ChooseAsync("Wii-U-Konto für " + p.Name, accounts.Select(a => $"{a.MiiName}   ({a.PersistentId})").ToList(),
            "Beim Start eines Wii-U-Spiels aktiviert der Hub dieses Cemu-Konto – so hat jedes Profil eigene Spielstände.");
        if (idx < 0)
            return;
        p.CemuAccount = accounts[idx].PersistentId;
        App.Hub.Profiles.Update(p);
        Build(keepFocus: true);
    }

    private async Task ChooseEdenAsync(UserProfile p)
    {
        var users = App.Hub.Miis.EdenUsers();
        if (users.Count == 0)
        {
            await Dialogs.MessageAsync("Eden-Benutzer", "Eden hat noch keine Benutzer – Eden einmal starten. Weitere Benutzer legst du in Eden unter " +
                                                        "Emulation → Konfigurieren → System → Profile an.");
            return;
        }
        var idx = await Dialogs.ChooseAsync("Switch-Benutzer für " + p.Name, users.Select(u => u.Name).ToList(),
            "Beim Start eines Switch-Spiels aktiviert der Hub diesen Eden-Benutzer – so hat jedes Profil eigene Spielstände.");
        if (idx < 0)
            return;
        p.EdenUser = users[idx].Uuid;
        App.Hub.Profiles.Update(p);
        Build(keepFocus: true);
    }

    private async Task RemoveAsync(UserProfile p)
    {
        if (!await Dialogs.ConfirmAsync("Profil löschen", $"Profil „{p.Name}“ löschen? Spielstand-Sicherungen und Statistik bleiben erhalten.",
                "Löschen"))
            return;
        App.Hub.Profiles.Remove(p);
        if (App.Hub.Profiles.Active.Id != App.Hub.Library.ActiveProfileId)
            App.Hub.Profiles.SetActive(App.Hub.Profiles.Active);
        Build();
    }

    public bool HandleNav(NavAction action) => false;
}
