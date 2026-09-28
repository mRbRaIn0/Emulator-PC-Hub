using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.WiiCompiled;
using EmulatorPCHub.Library;
using EmulatorPCHub.Mods;
using EmulatorPCHub.Updates;

namespace EmulatorPCHub.UI.Services;

/// <summary>
/// Kompositionswurzel des Hubs: verbindet Bibliothek, Adapter, Presets, Mods und Updates.
/// Das UI spricht nur mit diesem Objekt – nie direkt mit Emulatoren.
/// </summary>
public sealed class HubServices
{
    public AppPaths Paths { get; }
    public ConfigService Config { get; }
    public BackupService Backups { get; }
    public LaunchLog LaunchLog { get; }
    public LibraryService Library { get; }
    public AdapterRegistry Adapters { get; }
    public LaunchPipeline Pipeline { get; }
    public PresetService Presets { get; }
    public SwitchModManager SwitchMods { get; }
    public MarioKart8DeluxeService MarioKart8 { get; }
    public RetroRewindService RetroRewind { get; }
    public CemuGraphicPackService GraphicPacks { get; }
    public ComponentInstaller Installer { get; }
    public ProfileService Profiles { get; }
    public ControllerCoordinator Input { get; }
    public SaveManager Saves { get; }
    public MiiService Miis { get; }
    public StatisticsService Stats { get; }

    private HubServices(AppPaths paths)
    {
        Paths = paths;
        paths.EnsureCreated();
        HubLog.Initialize(paths.Logs);
        Backups = new BackupService(paths.Backups);
        Config = new ConfigService(paths, Backups);
        Config.Load();
        LaunchLog = new LaunchLog(paths.Logs);
        Library = new LibraryService(new LibraryDatabase(paths.LibraryDb), paths, Config);
        Adapters = new AdapterRegistry(paths, Config, Backups);
        Pipeline = new LaunchPipeline(LaunchLog);
        Presets = new PresetService(paths, Backups);
        SwitchMods = new SwitchModManager(paths, Backups, () => Adapters.Switch);
        MarioKart8 = new MarioKart8DeluxeService(SwitchMods, () => Adapters.Switch, Library);
        RetroRewind = new RetroRewindService(Adapters.Dolphin, paths);
        GraphicPacks = new CemuGraphicPackService(Adapters.Cemu, Backups);
        Installer = new ComponentInstaller(paths, Backups);
        Profiles = new ProfileService(paths, Config);
        Input = new ControllerCoordinator(this);
        Saves = new SaveManager(paths, Adapters);
        Miis = new MiiService(paths, Adapters, Backups);
        Stats = new StatisticsService(Library);
        Library.SetActiveProfile(Profiles.Active.Id);
        var activeProfile = Profiles.Active.Id;
        Profiles.Changed += (_, _) =>
        {
            if (Profiles.Active.Id == activeProfile)
                return;
            activeProfile = Profiles.Active.Id;
            OnProfileActivated(Profiles.Active);
        };
        Backups.Prune();
    }

    public static HubServices Create(AppPaths? paths = null) => new(paths ?? AppPaths.Detect());

    /// <summary>Scannt Bibliotheksordner und zusätzlich die in den Emulatoren eingetragenen Spieleordner.</summary>
    public Task<ScanResult> RefreshLibraryAsync(CancellationToken ct = default)
    {
        var extra = new List<ScanRoot>();
        foreach (var adapter in Adapters.All)
        {
            try
            {
                foreach (var dir in adapter.DetectGames())
                    extra.Add(new ScanRoot(dir, adapter.Platforms.Count == 1 ? adapter.Platforms[0] : null));
            }
            catch (Exception ex)
            {
                HubLog.Warn($"Spieleordner von {adapter.DisplayName} nicht lesbar", ex);
            }
        }
        var ww = Adapters.WheelWizard.ReadConfig().GameLocation;
        if (!string.IsNullOrWhiteSpace(ww) && File.Exists(ww))
            extra.Add(new ScanRoot(Path.GetDirectoryName(ww)!, HubPlatform.Wii));
        return Library.RefreshAsync(extra, ct);
    }

    // ------------------------------------------------------------------
    // Spielstart
    // ------------------------------------------------------------------

    public IEmulatorAdapter AdapterFor(GameEntry game, GamePreset? preset)
    {
        if (game.Special == SpecialPage.MarioKartWii)
        {
            var engine = Config.Current.MarioKartWii.Engine;
            return engine == EmulatorIds.Dolphin || preset?.Kind == PresetKind.Custom
                ? Adapters.Dolphin
                : Adapters.WiiCompiled;
        }
        return Adapters.ForGame(game) ?? throw new LaunchException($"Kein Emulator für {game.Platform.DisplayName()} verfügbar.");
    }

    public async Task<LaunchOutcome> LaunchAsync(GameEntry game, GamePreset? preset, IHubWindow? window,
        IProgress<LaunchProgress>? progress, CancellationToken ct = default)
    {
        IEmulatorAdapter adapter;
        try
        {
            adapter = AdapterFor(game, preset);
        }
        catch (LaunchException ex)
        {
            return new LaunchOutcome { Success = false, Error = ex.Message, Log = new LaunchLogEntry { Game = game.Title, Error = ex.Message } };
        }

        if (game.Special == SpecialPage.MarioKartWii && adapter is DolphinAdapter && preset?.Kind == PresetKind.Custom)
        {
            // Custom = Retro Rewind mit My-Stuff-Mods
            Config.Current.MarioKartWii.RetroRewindMyStuff = true;
            preset = preset.Clone();
            preset.Kind = PresetKind.RetroRewind;
        }

        Func<Task>? apply = null;
        if (game.Special == SpecialPage.MarioKart8Deluxe && preset != null)
        {
            apply = () => MarioKart8.ApplyPresetAsync(preset, Config.Current.MarioKart8Deluxe.CustomMods);
            // eigene Updates/DLCs vor dem Start eintragen
            try { MarioKart8.RegisterAddOns(); }
            catch (Exception ex) { HubLog.Warn("DLC/Update-Registrierung fehlgeschlagen", ex); }
        }
        if (adapter.Id == EmulatorIds.Switch && !string.IsNullOrWhiteSpace(Config.Current.LibraryPaths.Switch))
        {
            try { Adapters.Switch.AddGameFolder(Config.Current.LibraryPaths.Switch); }
            catch (Exception ex) { HubLog.Warn("Switch-Spieleordner konnte nicht eingetragen werden", ex); }
        }
        if (adapter.Id == EmulatorIds.Switch && game.Special != SpecialPage.MarioKart8Deluxe && game.GameCode != null
            && Library.SwitchAddOns.TryGetValue(game.GameCode, out var addOns))
        {
            // eigene Updates/DLCs auch für alle anderen Switch-Spiele eintragen
            try { SwitchAddOnRegistration.Register(Adapters.Switch, game.GameCode, addOns); }
            catch (Exception ex) { HubLog.Warn("DLC/Update-Registrierung fehlgeschlagen", ex); }
        }
        if (adapter.Id == EmulatorIds.Switch)
        {
            // neue/geänderte Miis aus dem Wii-Mii-Kanal automatisch auf die Switch übernehmen
            try { Miis.SyncWiiMiisToSwitch(); }
            catch (Exception ex) { HubLog.Warn("Mii-Abgleich mit Eden fehlgeschlagen", ex); }
        }

        if (preset != null && game.ActivePresetId != preset.Id)
        {
            game.ActivePresetId = preset.Id;
            Library.Save(game);
        }

        var profile = Profiles.Active;
        PrepareProfileForLaunch(adapter, profile);
        AutoSnapshot(game, profile, "Vor dem Spielen");

        var request = new LaunchRequest { Game = game, Preset = preset, Fullscreen = Config.Current.Launch.EmulatorFullscreen };
        var inputProgress = new Progress<string>(s => progress?.Report(new LaunchProgress(LaunchStep.LoadControllerProfile, s)));
        var outcome = await Pipeline.RunAsync(request, adapter, window, progress, apply,
            (started, duration, exit) => Library.RecordSession(game, preset?.Name, adapter.DisplayName, started, duration, exit), ct,
            loadControllers: () => Input.PrepareAsync(game, adapter, inputProgress),
            afterGame: Input.FinishAsync);
        Library.RecordLaunch(game, outcome.Started == default ? DateTimeOffset.Now : outcome.Started, outcome.Success);
        if (outcome.Duration > TimeSpan.Zero)
            AutoSnapshot(game, profile, "Nach dem Spielen");
        return outcome;
    }

    /// <summary>Profil → Emulator-Benutzer: Cemu-Konto bzw. Eden-Benutzer des Profils aktivieren (getrennte Spielstände).</summary>
    private void PrepareProfileForLaunch(IEmulatorAdapter adapter, UserProfile profile)
    {
        try
        {
            if (adapter.Id == EmulatorIds.Cemu && !string.IsNullOrWhiteSpace(profile.CemuAccount))
                Adapters.Cemu.SetActiveAccount(profile.CemuAccount);
            if (adapter is Emulation.Switch.EdenAdapter eden && Saves.EdenUserFor(profile) is { } user
                && !string.IsNullOrWhiteSpace(profile.EdenUser))
                eden.SetCurrentUser(user.Index);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Emulator-Benutzer des Profils konnte nicht gesetzt werden", ex);
        }
    }

    private void AutoSnapshot(GameEntry game, UserProfile profile, string label)
    {
        if (!Config.Current.Launch.AutoSaveSnapshots)
            return;
        try { Saves.CreateSnapshot(game, profile, label, auto: true); }
        catch (Exception ex) { HubLog.Warn($"Automatische Spielstand-Sicherung ({label}) fehlgeschlagen", ex); }
    }

    /// <summary>Beim Profilwechsel: Favoriten/Statistik umschalten und den bevorzugten Controller zu Spieler 1 machen.</summary>
    private void OnProfileActivated(UserProfile profile)
    {
        Library.SetActiveProfile(profile.Id);
        if (profile.PreferredController != null
            && Input.Controllers.Devices.FirstOrDefault(d => d.Key == profile.PreferredController) is { } device
            && device.Player != 1)
            Input.Controllers.SetPlayer(device, 1);
    }

    // ------------------------------------------------------------------
    // Integration / Automatische Einrichtung
    // ------------------------------------------------------------------

    /// <summary>
    /// Verbindet Wheel Wizard mit dem Dolphin des Hubs (Dolphin-Pfad, Userordner, Spiel),
    /// damit Retro Rewind/WiiCompiled ohne manuelle Einrichtung funktionieren.
    /// </summary>
    public void ConfigureWheelWizard()
    {
        var dolphin = Adapters.Dolphin.DetectInstallation();
        if (!dolphin.IsInstalled || Adapters.WheelWizard.FindExecutable() == null)
            return;
        var game = Library.Find(KnownGames.MarioKartWiiId);
        var gameFile = game is { IsPlaceholder: false } ? game.Path : null;
        Adapters.WheelWizard.WriteConfig(dolphin.ExecutablePath, dolphin.UserDataDir, gameFile);
    }

    /// <summary>
    /// Ersteinrichtung des Switch-Emulators: startet ihn einmal normal (dort eigene Keys und Firmware einspielen).
    /// Danach trägt der Hub den Switch-Bibliotheksordner (Spiele + automatisches Laden von Updates/DLCs) ein,
    /// registriert gefundene Updates/DLCs und kann ab jetzt die Controller-Belegung schreiben.
    /// </summary>
    public async Task<string> RunSwitchEmulatorSetupAsync(IHubWindow? window)
    {
        var adapter = Adapters.Switch;
        var install = adapter.DetectInstallation();
        if (!install.IsInstalled)
            return "Der Switch-Emulator ist nicht installiert (Komponenten → Eden).";
        window?.HideForGame();
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(install.ExecutablePath!)
            {
                WorkingDirectory = Path.GetDirectoryName(install.ExecutablePath),
                UseShellExecute = false,
            });
            if (p != null)
                await p.WaitForExitAsync();
        }
        finally
        {
            window?.RestoreAfterGame();
        }
        return FinishSwitchSetup();
    }

    /// <summary>Bibliotheksordner und eigene Updates/DLCs beim Switch-Emulator eintragen (sofern dessen Config existiert).</summary>
    public string FinishSwitchSetup()
    {
        var adapter = Adapters.Switch;
        var folder = Config.Current.LibraryPaths.Switch;
        try
        {
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                adapter.AddGameFolder(folder);
            SwitchAddOnRegistration.RegisterAll(adapter, Library);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Switch-Einrichtung unvollständig", ex);
        }
        var sys = adapter.GetSystemStatus();
        return (sys.KeysPresent, sys.FirmwarePresent) switch
        {
            (true, true) => "Switch-Emulator eingerichtet ✓ Keys und Firmware vorhanden.",
            (false, _) => $"Noch keine Keys: eigene prod.keys nach {sys.KeysPath} kopieren.",
            _ => "Keys vorhanden – jetzt noch die eigene Firmware im Switch-Emulator installieren (Eden: Tools → Install Firmware).",
        };
    }

    /// <summary>Installiert WiiCompiled mit dem Mario-Kart-Wii-Dump des Benutzers.</summary>
    public async Task<int> InstallWiiCompiledAsync(IProgress<WiiCompiledProgress> progress, CancellationToken ct)
    {
        var game = Library.Find(KnownGames.MarioKartWiiId);
        var problem = WiiCompiledAdapter.ValidateDump(game);
        if (problem != null)
            throw new LaunchException(problem);
        var rr = RetroRewind.Layout;
        return await Adapters.WiiCompiled.InstallAsync(game!.Path, rr?.DataFolder, progress, ct);
    }

    /// <summary>Alle Komponenten mit installiertem Stand (ohne Netzwerk).</summary>
    public List<ComponentInfo> ComponentStatus()
    {
        var list = new List<ComponentInfo>();
        foreach (var def in ComponentInstaller.Catalog)
        {
            var info = new ComponentInfo { Id = def.Id, Name = def.Name, Category = def.Category, Homepage = def.Homepage, Details = def.Description };
            try
            {
                switch (def.Id)
                {
                    case ComponentIds.RetroRewind:
                        var rr = RetroRewind.LocalStatus();
                        info.Status = rr.Installed ? Core.Models.ComponentStatus.Installed : Core.Models.ComponentStatus.NotInstalled;
                        info.InstalledVersion = rr.Version;
                        info.InstallPath = rr.Folder;
                        break;
                    case ComponentIds.CtgpDeluxe:
                        var ctgp = MarioKart8.CtgpStatus();
                        info.Status = ctgp.Installed ? Core.Models.ComponentStatus.Installed : Core.Models.ComponentStatus.NotInstalled;
                        info.InstalledVersion = ctgp.Version;
                        info.InstallPath = ctgp.Path;
                        break;
                    case ComponentIds.PadForge:
                        var pf = Input.PadForge.Status();
                        info.Status = pf.Installed ? Core.Models.ComponentStatus.Installed : Core.Models.ComponentStatus.NotInstalled;
                        info.InstalledVersion = pf.Version;
                        info.InstallPath = pf.ExePath;
                        if (pf.Installed && !pf.FirstRunDone)
                        {
                            info.Status = Core.Models.ComponentStatus.NeedsAttention;
                            info.Problems.Add("Ersteinrichtung nötig: einmal starten (Controller-Seite → PadForge einrichten), installiert den HIDMaestro-Treiber.");
                        }
                        break;
                    case ComponentIds.WheelWizard:
                        var ww = Adapters.WheelWizard.FindExecutable();
                        info.Status = ww != null ? Core.Models.ComponentStatus.Installed : Core.Models.ComponentStatus.NotInstalled;
                        info.InstalledVersion = Adapters.WheelWizard.Version();
                        info.InstallPath = ww;
                        break;
                    default:
                        var adapter = Adapters.Get(def.Id);
                        var inst = adapter?.DetectInstallation() ?? EmulatorInstallation.NotFound;
                        info.Status = inst.IsInstalled ? Core.Models.ComponentStatus.Installed : Core.Models.ComponentStatus.NotInstalled;
                        info.InstalledVersion = inst.Version;
                        info.InstallPath = inst.ExecutablePath;
                        info.Problems.AddRange(inst.Problems);
                        if (inst.IsInstalled && inst.Problems.Count > 0)
                            info.Status = Core.Models.ComponentStatus.NeedsAttention;
                        if (!inst.IsInstalled && def.Id == ComponentIds.WiiCompiled && Adapters.WiiCompiled.DownloadedSetup != null)
                        {
                            info.Status = Core.Models.ComponentStatus.NeedsAttention;
                            info.Problems.Add("Setup liegt bereit – Installation braucht deinen Mario-Kart-Wii-PAL-Dump (RMCP01).");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                info.Status = Core.Models.ComponentStatus.Unknown;
                info.Problems.Add(ex.Message);
            }
            list.Add(info);
        }
        return list;
    }
}
