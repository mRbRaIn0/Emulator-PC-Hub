using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Updates;
using EmulatorPCHub.UI.Services;

namespace EmulatorPCHub.UI.ViewModels;

public sealed partial class ComponentItemViewModel : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public string Homepage { get; init; } = "";
    public string SourceText { get; init; } = "";

    [ObservableProperty] private ComponentStatus _status;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _installedVersion = "";
    [ObservableProperty] private string _latestVersion = "";
    [ObservableProperty] private string _changelog = "";
    [ObservableProperty] private string _problems = "";
    [ObservableProperty] private string? _installPath;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _actionText = "Aus Datei installieren …";
    [ObservableProperty] private bool _canAct = true;

    public ReleaseInfo? Latest { get; set; }
    public bool IsInstalled => Status is ComponentStatus.Installed or ComponentStatus.UpdateAvailable or ComponentStatus.NeedsAttention;
    public string Glyph => Status switch
    {
        ComponentStatus.Installed => "✓",
        ComponentStatus.UpdateAvailable => "⬆",
        ComponentStatus.NeedsAttention => "!",
        _ => "✕",
    };
    public string AccentColor => Status switch
    {
        ComponentStatus.Installed => "#FF2ECC71",
        ComponentStatus.UpdateAvailable => "#FF3FA9F5",
        ComponentStatus.NeedsAttention => "#FFF39C12",
        _ => "#FF8A8A8A",
    };

    partial void OnStatusChanged(ComponentStatus value)
    {
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(IsInstalled));
    }
}

/// <summary>Komponenten-Seite + Update-Manager (Plan Abschnitte 15/16).</summary>
public sealed partial class ComponentsViewModel : ObservableObject
{
    private readonly HubServices _hub;
    public ObservableCollection<ComponentItemViewModel> Items { get; } = [];

    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private string _summary = "";

    public ComponentsViewModel(HubServices hub)
    {
        _hub = hub;
    }

    public void Reload()
    {
        var status = _hub.ComponentStatus();
        foreach (var info in status)
        {
            var def = ComponentInstaller.Find(info.Id)!;
            var item = Items.FirstOrDefault(i => i.Id == info.Id);
            if (item == null)
            {
                item = new ComponentItemViewModel
                {
                    Id = info.Id,
                    Name = info.Name,
                    Category = info.Category,
                    Description = def.Description,
                    Homepage = def.Homepage,
                    SourceText = def.Source?.Description ?? "",
                };
                Items.Add(item);
            }
            Apply(item, info);
        }
        UpdateSummary();
    }

    private static void Apply(ComponentItemViewModel item, ComponentInfo info)
    {
        item.InstalledVersion = info.InstalledVersion ?? "";
        item.InstallPath = info.InstallPath;
        item.Problems = string.Join("\n", info.Problems);
        var status = info.Status;
        if (status == ComponentStatus.Installed && item.Latest != null && info.InstalledVersion != null &&
            Mods.RetroRewindStatus.CompareVersions(item.Latest.Version, info.InstalledVersion) > 0 &&
            !info.InstalledVersion.Contains(item.Latest.Version, StringComparison.OrdinalIgnoreCase))
            status = ComponentStatus.UpdateAvailable;
        item.Status = status;
        item.StatusText = status switch
        {
            ComponentStatus.Installed => "Installiert ✓",
            ComponentStatus.UpdateAvailable => $"Update verfügbar: {item.Latest?.Version}",
            ComponentStatus.NeedsAttention => "Installiert – prüfen",
            ComponentStatus.NotInstalled => "Nicht installiert",
            _ => "Unbekannt",
        };
        item.ActionText = status switch
        {
            ComponentStatus.UpdateAvailable => "Update aus Datei …",
            ComponentStatus.Installed or ComponentStatus.NeedsAttention => "Reparieren aus Datei …",
            _ => "Aus Datei installieren …",
        };
    }

    private void UpdateSummary()
    {
        var installed = Items.Count(i => i.IsInstalled);
        var updates = Items.Count(i => i.Status == ComponentStatus.UpdateAvailable);
        Summary = $"{installed} von {Items.Count} Komponenten installiert" + (updates > 0 ? $" · {updates} Update(s)" : "");
    }

    /// <summary>Fragt die offiziellen Quellen nach neuen Versionsnummern (keine Downloads).</summary>
    public async Task CheckUpdatesAsync()
    {
        IsChecking = true;
        try
        {
            var tasks = Items.Select(async item =>
            {
                var def = ComponentInstaller.Find(item.Id);
                if (def?.Source == null)
                    return;
                try
                {
                    item.Latest = await def.Source.LatestAsync(CancellationToken.None);
                    item.LatestVersion = item.Latest?.Version ?? "";
                    item.Changelog = item.Latest?.Changelog?.Trim() ?? "";
                }
                catch (Exception ex)
                {
                    item.LatestVersion = "";
                    item.Changelog = $"Server nicht erreichbar ({def.Source.Description}): {ex.Message}";
                    HubLog.Warn($"Update-Check {item.Name} fehlgeschlagen", ex);
                }
            }).ToList();
            await Task.WhenAll(tasks);
            Reload();
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Lokal abgelegte Pakete je Komponente (Pfad, gültig?).</summary>
    public Dictionary<string, IReadOnlyList<ComponentInstaller.LocalPackage>> LocalPackages { get; } = [];

    public async Task ScanLocalPackagesAsync()
    {
        foreach (var item in Items)
            LocalPackages[item.Id] = await _hub.Installer.FindLocalPackagesAsync(item.Id);
    }

    /// <summary>Installiert eine vom Nutzer selbst heruntergeladene Datei (einziger Installationsweg).</summary>
    public async Task<string?> InstallFromFileAsync(ComponentItemViewModel item, string file)
    {
        item.IsBusy = true;
        item.CanAct = false;
        try
        {
            switch (item.Id)
            {
                case ComponentIds.RetroRewind:
                    await _hub.RetroRewind.InstallFromFileAsync(file, new Progress<string>(s => item.ProgressText = s), CancellationToken.None);
                    break;
                case ComponentIds.CtgpDeluxe:
                    await _hub.SwitchMods.InstallAsync(file, KnownGames.MarioKart8DeluxeTitleId,
                        Mods.MarioKart8DeluxeService.CtgpFolderName, new Progress<string>(s => item.ProgressText = s));
                    break;
                default:
                    await _hub.Installer.InstallFromFileAsync(item.Id, file,
                        new Progress<InstallProgress>(p => item.ProgressText = p.Text), CancellationToken.None);
                    if (item.Id is ComponentIds.Dolphin or ComponentIds.WheelWizard)
                        _hub.ConfigureWheelWizard();
                    break;
            }
            item.ProgressText = "Fertig ✓";
            return null;
        }
        catch (Exception ex)
        {
            HubLog.Warn($"Installation aus Datei ({item.Name}) fehlgeschlagen", ex);
            item.ProgressText = "Fehler";
            return ex.Message;
        }
        finally
        {
            item.IsBusy = false;
            item.CanAct = true;
            Reload();
            await ScanLocalPackagesAsync();
        }
    }
}
