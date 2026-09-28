using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Library;
using EmulatorPCHub.Mods;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.UI.ViewModels;

/// <summary>Ein Mod-Eintrag mit Schalter (für das Custom-Preset).</summary>
public sealed partial class ModToggleItem : ObservableObject
{
    public required ModInfo Mod { get; init; }
    public string Name => Mod.Name;
    public string VersionText => Mod.Version is { } v ? $"v{v}" : "";
    [ObservableProperty] private bool _isChecked;
}

/// <summary>
/// Mario-Kart-8-Deluxe-Seite (Plan Abschnitt 8): Edition Vanilla / CTGP Deluxe / Custom, Status von
/// Spiel, Keys, Firmware, eigenen Updates und DLCs (Booster-Streckenpass) sowie CTGP-Kompatibilität.
/// </summary>
public sealed partial class MarioKart8DeluxeViewModel : ObservableObject
{
    private readonly HubServices _hub;

    public ObservableCollection<OptionItem> Editions { get; } = [];
    public ObservableCollection<ModToggleItem> CustomMods { get; } = [];

    [ObservableProperty] private string _gameText = "";
    [ObservableProperty] private bool _hasGame;
    [ObservableProperty] private string _emulatorText = "";
    [ObservableProperty] private string _keysText = "";
    [ObservableProperty] private string _firmwareText = "";
    [ObservableProperty] private string _ctgpText = "";
    [ObservableProperty] private string _ctgpVersionText = "";
    [ObservableProperty] private string _dlcText = "";
    [ObservableProperty] private string _updateText = "";
    [ObservableProperty] private string _compatText = "";
    [ObservableProperty] private string _readyText = "";
    [ObservableProperty] private bool _canPlay;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";
    [ObservableProperty] private string _playTimeText = "";
    [ObservableProperty] private bool _showCustomMods;

    public GameEntry Game => _hub.Library.Find(KnownGames.MarioKart8DeluxeId)!;

    public MarioKart8DeluxeViewModel(HubServices hub)
    {
        _hub = hub;
    }

    public GamePreset SelectedPreset =>
        _hub.Presets.Active(Game, Editions.FirstOrDefault(e => e.IsSelected)?.Id ?? _hub.Config.Current.MarioKart8Deluxe.Preset);

    public void Reload()
    {
        var game = Game;
        var cfg = _hub.Config.Current.MarioKart8Deluxe;
        HasGame = !game.IsPlaceholder;
        GameText = HasGame ? Path.GetFileName(game.Path) : "Kein eigener Mario-Kart-8-Deluxe-Dump (NSP/XCI) gefunden";
        PlayTimeText = Format.PlayTime(game.PlayTimeSeconds);

        var adapter = _hub.Adapters.Switch;
        var install = adapter.DetectInstallation();
        EmulatorText = install.IsInstalled ? $"{adapter.DisplayName} {install.Version}" : $"{adapter.DisplayName} nicht installiert";
        var sys = adapter.GetSystemStatus();
        KeysText = sys.KeysPresent ? "Keys vorhanden ✓" : "Keys fehlen – eigene prod.keys aus deiner Switch nötig";
        FirmwareText = sys.FirmwarePresent ? "Firmware installiert ✓" : "Firmware fehlt – eigene Firmware in Eden installieren (Tools → Install Firmware)";

        var ctgp = _hub.MarioKart8.CtgpStatus();
        CtgpText = ctgp.Installed ? (ctgp.Enabled ? "CTGP Deluxe aktiv" : "CTGP Deluxe bereit") : "CTGP Deluxe nicht installiert";
        CtgpVersionText = ctgp.Version is { } v ? $"Version {v}" : "";

        var addons = _hub.MarioKart8.AddOnStatus();
        DlcText = addons.DlcFilesFound == 0
            ? "Keine eigenen DLC-Dateien gefunden"
            : $"{addons.DlcFilesFound} DLC-Datei(en) gefunden, {addons.DlcRegistered} im Emulator eingetragen";
        UpdateText = addons.UpdateFile == null
            ? "Kein Update gefunden (CTGP Deluxe braucht Version 3.0.3)"
            : $"Update: {addons.UpdateVersionText ?? Path.GetFileName(addons.UpdateFile)}";
        CompatText = addons.UpdateFile == null ? "" : addons.UpdateMatchesCtgp
            ? "Spielversion passt zu CTGP Deluxe ✓"
            : $"Hinweis: CTGP Deluxe 1.1.1 funktioniert nur mit Version {MarioKart8DeluxeService.CtgpRequiredGameVersion}";

        Editions.Clear();
        foreach (var p in _hub.Presets.ForGame(game))
        {
            var item = new OptionItem
            {
                Id = p.Id,
                Title = p.Name,
                Subtitle = p.Description,
                Features = p.Features,
                IsSelected = p.Id == cfg.Preset,
            };
            if (p.Kind == PresetKind.CtgpDeluxe && !ctgp.Installed)
            {
                item.IsAvailable = false;
                item.Status = "CTGP Deluxe installieren";
            }
            Editions.Add(item);
        }
        if (!Editions.Any(e => e.IsSelected) && Editions.Count > 0)
            Editions[0].IsSelected = true;

        CustomMods.Clear();
        foreach (var mod in _hub.SwitchMods.List(KnownGames.MarioKart8DeluxeTitleId, game.Title))
            CustomMods.Add(new ModToggleItem { Mod = mod, IsChecked = cfg.CustomMods.Contains(mod.Name) });
        ShowCustomMods = Editions.FirstOrDefault(e => e.IsSelected)?.Id == "mk8dx-custom";

        if (!install.IsInstalled)
            (ReadyText, CanPlay) = ("Switch-Emulator unter Komponenten installieren", false);
        else if (!HasGame)
            (ReadyText, CanPlay) = ("Eigenen Dump hinzufügen, um zu spielen", false);
        else if (!sys.KeysPresent)
            (ReadyText, CanPlay) = ("Eigene Keys fehlen (prod.keys)", false);
        else if (Editions.FirstOrDefault(e => e.IsSelected) is { IsAvailable: false } ed)
            (ReadyText, CanPlay) = (ed.Status, false);
        else
            (ReadyText, CanPlay) = ("Status: Bereit", true);
    }

    public void SelectEdition(OptionItem item)
    {
        foreach (var e in Editions)
            e.IsSelected = e == item;
        _hub.Config.Update(c => c.MarioKart8Deluxe.Preset = item.Id);
        Reload();
    }

    public void SaveCustomMods()
    {
        _hub.Config.Update(c => c.MarioKart8Deluxe.CustomMods = CustomMods.Where(m => m.IsChecked).Select(m => m.Name).ToList());
    }

    public string? SetGameFile(string path)
    {
        var info = SwitchFileReader.Read(path);
        if (info.TitleId != null && !KnownGames.IsMarioKart8DeluxeCode(info.BaseTitleId))
            return $"Diese Datei gehört nicht zu Mario Kart 8 Deluxe (Title-ID {info.TitleId}).";
        _hub.Config.Update(c => c.MarioKart8Deluxe.GameFile = path);
        var game = Game;
        game.Path = path;
        game.IsPlaceholder = false;
        _hub.Library.Save(game);
        Reload();
        return null;
    }

    public async Task<string?> ImportCtgpAsync(string zipOrFolder)
    {
        IsBusy = true;
        BusyText = "CTGP Deluxe wird importiert …";
        try
        {
            await _hub.SwitchMods.InstallAsync(zipOrFolder, KnownGames.MarioKart8DeluxeTitleId,
                MarioKart8DeluxeService.CtgpFolderName, new Progress<string>(s => BusyText = s));
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            IsBusy = false;
            Reload();
        }
    }

    public string RegisterAddOns()
    {
        try
        {
            var n = _hub.MarioKart8.RegisterAddOns();
            Reload();
            return n == 0 ? "Keine eigenen Update-/DLC-Dateien gefunden." : $"{n} Datei(en) im Switch-Emulator eingetragen.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
