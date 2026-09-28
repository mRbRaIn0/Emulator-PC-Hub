using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.WiiCompiled;
using EmulatorPCHub.Mods;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.UI.ViewModels;

/// <summary>
/// Mario-Kart-Wii-Seite (Plan 6.1): Edition (Vanilla / Retro Rewind / eigenes Preset),
/// Engine (WiiCompiled / Dolphin), Status aller Komponenten, Installation von WiiCompiled.
/// </summary>
public sealed partial class MarioKartWiiViewModel : ObservableObject
{
    private readonly HubServices _hub;

    public ObservableCollection<OptionItem> Editions { get; } = [];
    public ObservableCollection<OptionItem> Engines { get; } = [];

    [ObservableProperty] private string _dumpText = "";
    [ObservableProperty] private bool _hasDump;
    [ObservableProperty] private string _retroRewindText = "";
    [ObservableProperty] private string _wiiCompiledText = "";
    [ObservableProperty] private string _wheelWizardText = "";
    [ObservableProperty] private string _dolphinText = "";
    [ObservableProperty] private bool _canInstallWiiCompiled;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";
    [ObservableProperty] private double _busyPercent;
    [ObservableProperty] private string _playTimeText = "";
    [ObservableProperty] private string _controllerText = "Tastatur / Controller";
    [ObservableProperty] private string _readyText = "";
    [ObservableProperty] private bool _canPlay;
    [ObservableProperty] private bool _myStuff;
    [ObservableProperty] private bool _separateSave;
    [ObservableProperty] private bool _retroRewindInstalled;
    [ObservableProperty] private bool _retroRewindUpdate;
    [ObservableProperty] private bool _wiiCompiledInstalled;
    [ObservableProperty] private bool _dolphinInstalled;
    [ObservableProperty] private bool _wheelWizardInstalled;

    public GameEntry Game => _hub.Library.Find(KnownGames.MarioKartWiiId)!;

    public MarioKartWiiViewModel(HubServices hub)
    {
        _hub = hub;
    }

    public GamePreset SelectedPreset =>
        _hub.Presets.Active(Game, Editions.FirstOrDefault(e => e.IsSelected)?.Id ?? _hub.Config.Current.MarioKartWii.Preset);

    public void Reload()
    {
        var cfg = _hub.Config.Current.MarioKartWii;
        var game = Game;
        HasDump = !game.IsPlaceholder;
        DumpText = HasDump
            ? $"{Path.GetFileName(game.Path)} ({game.GameCode ?? "?"})"
            : "Kein eigener Mario-Kart-Wii-Dump gefunden";
        PlayTimeText = Format.PlayTime(game.PlayTimeSeconds);
        MyStuff = cfg.RetroRewindMyStuff;
        SeparateSave = cfg.RetroRewindSeparateSave;

        var rr = _hub.RetroRewind.LocalStatus();
        RetroRewindText = rr.Installed ? $"Retro Rewind {rr.Version} installiert" : "Retro Rewind nicht installiert";
        RetroRewindInstalled = rr.Installed;

        var wc = _hub.Adapters.WiiCompiled.FindInstall();
        var dumpProblem = WiiCompiledAdapter.ValidateDump(HasDump ? game : null);
        WiiCompiledText = wc != null
            ? $"WiiCompiled {wc.Version} installiert"
            : _hub.Adapters.WiiCompiled.DownloadedSetup != null
                ? (dumpProblem == null ? "WiiCompiled bereit zur Installation" : "WiiCompiled: " + dumpProblem)
                : "WiiCompiled nicht heruntergeladen";
        WiiCompiledInstalled = wc != null;
        CanInstallWiiCompiled = wc == null && dumpProblem == null && _hub.Adapters.WiiCompiled.DownloadedSetup != null;

        var dolphin = _hub.Adapters.Dolphin.DetectInstallation();
        DolphinInstalled = dolphin.IsInstalled;
        DolphinText = dolphin.IsInstalled ? $"Dolphin {dolphin.Version} installiert" : "Dolphin nicht installiert";
        var ww = _hub.Adapters.WheelWizard.FindExecutable();
        WheelWizardInstalled = ww != null;
        WheelWizardText = ww != null ? $"Wheel Wizard {_hub.Adapters.WheelWizard.Version()} (Advanced Tools)" : "Wheel Wizard nicht installiert";

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
            if (p.Kind is PresetKind.RetroRewind or PresetKind.Custom && !rr.Installed)
            {
                item.IsAvailable = false;
                item.Status = "Retro Rewind installieren";
            }
            Editions.Add(item);
        }
        if (!Editions.Any(e => e.IsSelected) && Editions.Count > 0)
            Editions[0].IsSelected = true;

        Engines.Clear();
        Engines.Add(new OptionItem
        {
            Id = "wiicompiled",
            Title = "WiiCompiled",
            Subtitle = "Nativ auf dem PC (empfohlen)",
            IsSelected = cfg.Engine != "dolphin",
            IsAvailable = wc != null,
            Status = wc != null ? $"v{wc.Version}" : "nicht installiert",
        });
        Engines.Add(new OptionItem
        {
            Id = "dolphin",
            Title = "Dolphin",
            Subtitle = "Emulation (alle Regionen, Fallback)",
            IsSelected = cfg.Engine == "dolphin",
            IsAvailable = dolphin.IsInstalled,
            Status = dolphin.IsInstalled ? dolphin.Version ?? "" : "nicht installiert",
        });
        UpdateReady();
    }

    public void SelectEdition(OptionItem item)
    {
        foreach (var e in Editions)
            e.IsSelected = e == item;
        _hub.Config.Update(c => c.MarioKartWii.Preset = item.Id);
        UpdateReady();
    }

    public void SelectEngine(OptionItem item)
    {
        foreach (var e in Engines)
            e.IsSelected = e == item;
        _hub.Config.Update(c => c.MarioKartWii.Engine = item.Id);
        var game = Game;
        game.EmulatorId = item.Id == "dolphin" ? EmulatorIds.Dolphin : EmulatorIds.WiiCompiled;
        _hub.Library.Save(game);
        UpdateReady();
    }

    partial void OnMyStuffChanged(bool value) => _hub.Config.Update(c => c.MarioKartWii.RetroRewindMyStuff = value);
    partial void OnSeparateSaveChanged(bool value) => _hub.Config.Update(c => c.MarioKartWii.RetroRewindSeparateSave = value);

    private void UpdateReady()
    {
        var engine = Engines.FirstOrDefault(e => e.IsSelected);
        var edition = Editions.FirstOrDefault(e => e.IsSelected);
        if (!HasDump && engine?.Id == "dolphin")
        {
            ReadyText = "Eigenen Dump auswählen, um zu spielen";
            CanPlay = false;
        }
        else if (engine is { IsAvailable: false })
        {
            ReadyText = engine.Id == "wiicompiled"
                ? (HasDump ? "WiiCompiled zuerst installieren (oder Engine Dolphin wählen)" : "Eigenen PAL-Dump hinzufügen, dann WiiCompiled installieren")
                : "Dolphin unter Komponenten installieren";
            CanPlay = false;
        }
        else if (edition is { IsAvailable: false })
        {
            ReadyText = edition.Status;
            CanPlay = false;
        }
        else
        {
            ReadyText = $"Bereit: {edition?.Title} mit {engine?.Title}";
            CanPlay = true;
        }
    }

    /// <summary>Setzt den eigenen Dump (Datei vom Benutzer ausgewählt).</summary>
    public string? SetDump(string path)
    {
        var info = Library.DiscHeaderReader.Read(path);
        if (info == null || !KnownGames.IsMarioKartWiiCode(info.GameCode))
            return "Diese Datei ist kein Mario-Kart-Wii-Image (erwartet Disc-ID RMC…).";
        _hub.Config.Update(c => c.MarioKartWii.GameFile = path);
        var game = Game;
        game.Path = path;
        game.GameCode = info.GameCode;
        game.IsPlaceholder = false;
        _hub.Library.Save(game);
        _hub.ConfigureWheelWizard();
        Reload();
        return null;
    }

    public async Task<string?> InstallWiiCompiledAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        BusyText = "WiiCompiled wird installiert – das kann eine Weile dauern …";
        try
        {
            var progress = new Progress<WiiCompiledProgress>(p =>
            {
                BusyText = p.Message.Length > 140 ? p.Message[..140] + "…" : p.Message;
                if (p.Percent is { } pct)
                    BusyPercent = pct;
            });
            var code = await _hub.InstallWiiCompiledAsync(progress, ct);
            return code == 0 ? null : $"WiiCompiled-Setup beendet mit Code {code}. Details unter Einstellungen → Logs.";
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

    public async Task<string?> InstallRetroRewindAsync(string zip, CancellationToken ct = default)
    {
        IsBusy = true;
        BusyText = "Retro Rewind wird installiert …";
        try
        {
            await _hub.RetroRewind.InstallFromFileAsync(zip, new Progress<string>(s => BusyText = s), ct);
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

    public async Task CheckRetroRewindUpdateAsync()
    {
        var status = await _hub.RetroRewind.StatusAsync();
        RetroRewindUpdate = status.UpdateAvailable;
        if (status.Installed)
            RetroRewindText = status.UpdateAvailable
                ? $"Retro Rewind {status.Version} – Update {status.LatestVersion} verfügbar"
                : $"Retro Rewind {status.Version} – aktuell";
    }
}
