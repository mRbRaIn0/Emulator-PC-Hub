using System.Text.RegularExpressions;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Mods;

public sealed record CtgpStatus(bool Installed, bool Enabled, string? Version, string? Path, long SizeBytes);

public sealed record Mk8AddOnStatus(
    int DlcFilesFound,
    int DlcRegistered,
    string? UpdateFile,
    string? UpdateVersionText,
    bool UpdateMatchesCtgp);

/// <summary>
/// Mario Kart 8 Deluxe + CTGP Deluxe (Plan Abschnitt 8) sowie eigene Updates/DLCs (Booster-Streckenpass).
/// CTGP Deluxe wirkt wie eine eigene Edition: Aktivieren/Deaktivieren ohne manuelles Verschieben.
/// </summary>
public sealed partial class MarioKart8DeluxeService
{
    public const string TitleId = KnownGames.MarioKart8DeluxeTitleId;
    public const string CtgpFolderName = "CTGP-DX";
    /// <summary>CTGP Deluxe 1.1.x setzt Spielversion 3.0.3 voraus.</summary>
    public const string CtgpRequiredGameVersion = "3.0.3";

    private readonly SwitchModManager _mods;
    private readonly Func<SwitchEmulatorAdapter> _adapter;
    private readonly LibraryService _library;

    public MarioKart8DeluxeService(SwitchModManager mods, Func<SwitchEmulatorAdapter> adapter, LibraryService library)
    {
        _mods = mods;
        _adapter = adapter;
        _library = library;
    }

    public ModInfo? FindCtgp() =>
        _mods.List(TitleId).FirstOrDefault(m => m.Name.Contains("CTGP", StringComparison.OrdinalIgnoreCase));

    public CtgpStatus CtgpStatus()
    {
        var mod = FindCtgp();
        return mod == null
            ? new CtgpStatus(false, false, null, null, 0)
            : new CtgpStatus(true, mod.Enabled, mod.Version, mod.Path, 0);
    }

    /// <summary>Aktiviert die Mods passend zum Preset (Vanilla = keine, CTGP = nur CTGP, Custom = Auswahl).</summary>
    public async Task ApplyPresetAsync(GamePreset preset, IReadOnlyCollection<string> customMods, IProgress<string>? progress = null)
    {
        IReadOnlyCollection<string> wanted = preset.Kind switch
        {
            PresetKind.CtgpDeluxe => FindCtgp() is { } c ? [c.Name]
                : throw new Emulation.LaunchException("CTGP Deluxe ist nicht installiert (Mods → CTGP Deluxe importieren)."),
            PresetKind.Custom => customMods,
            _ => preset.Mods,
        };
        await _mods.ActivateExactlyAsync(TitleId, wanted, progress);
        HubLog.Info($"MK8DX-Preset {preset.Name}: aktive Mods = {string.Join(", ", wanted)}");
    }

    public IReadOnlyList<GameFileInfo> DlcFiles() =>
        _library.SwitchAddOns.TryGetValue(TitleId, out var a) ? a.Dlcs : [];

    public IReadOnlyList<GameFileInfo> UpdateFiles() =>
        _library.SwitchAddOns.TryGetValue(TitleId, out var a) ? a.Updates : [];

    [GeneratedRegex(@"(?<!\d)(\d+\.\d+\.\d+)(?!\d)")]
    private static partial Regex DisplayVersion();

    public static string? VersionText(GameFileInfo? update)
    {
        if (update == null)
            return null;
        var m = DisplayVersion().Match(Path.GetFileName(update.Path));
        if (m.Success)
            return m.Groups[1].Value;
        return update.Version is { } v ? $"v{v}" : null;
    }

    public Mk8AddOnStatus AddOnStatus()
    {
        var dlcs = DlcFiles();
        var update = UpdateFiles().OrderBy(u => u.Version ?? 0).LastOrDefault();
        var registered = 0;
        try
        {
            registered = _adapter().RegisteredDlcPaths(TitleId).Count;
        }
        catch (Exception)
        {
        }
        var versionText = VersionText(update);
        return new Mk8AddOnStatus(dlcs.Count, registered, update?.Path, versionText, versionText == CtgpRequiredGameVersion);
    }

    /// <summary>Trägt alle gefundenen eigenen Updates und DLCs im Switch-Emulator ein.</summary>
    public int RegisterAddOns() =>
        _library.SwitchAddOns.TryGetValue(TitleId, out var a)
            ? SwitchAddOnRegistration.Register(_adapter(), TitleId, a)
            : SwitchAddOnRegistration.Register(_adapter(), TitleId, new SwitchAddOns());
}
