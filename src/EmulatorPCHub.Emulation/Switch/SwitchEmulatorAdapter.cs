using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Switch;

/// <summary>Lokaler Status von Keys/Firmware – es wird nur geprüft, nie etwas geladen (Plan Abschnitt 26).</summary>
public sealed record SwitchSystemStatus(bool KeysPresent, bool FirmwarePresent, string KeysPath, string FirmwarePath);

/// <summary>
/// Basis für Switch-Emulatoren (Plan 5.4). Der konkrete Emulator ist austauschbar –
/// Eden ist der Standard, weitere Emulatoren können als eigener Adapter ergänzt werden.
/// </summary>
public abstract class SwitchEmulatorAdapter : AdapterBase
{
    protected SwitchEmulatorAdapter(AppPaths paths, ConfigService config) : base(paths, config) { }

    public override string Id => EmulatorIds.Switch;
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.Switch];

    /// <summary>Kurzname des konkreten Emulators (z. B. "eden").</summary>
    public abstract string ImplementationId { get; }

    public abstract string DataDirectory(string? exe = null);

    public abstract SwitchSystemStatus GetSystemStatus();

    /// <summary>Ordner, in dem aktive Mods für eine Title-ID liegen.</summary>
    public abstract string ModsDirectory(string titleId);

    /// <summary>Trägt Updates und DLCs (eigene Dumps) für ein Spiel im Emulator ein.</summary>
    public abstract void RegisterAddOns(string titleId, IEnumerable<SwitchAddOnFile> updates, IEnumerable<SwitchAddOnFile> dlcs);

    public abstract IReadOnlyList<string> RegisteredDlcPaths(string titleId);
    public abstract string? SelectedUpdatePath(string titleId);

    /// <summary>Fügt einen Ordner zu Spiele- und Autoload-Ordnern (Updates/DLC) des Emulators hinzu.</summary>
    public abstract void AddGameFolder(string folder);
}

/// <summary>Update- oder DLC-Datei (Pfad, Title-ID, enthaltene NCAs).</summary>
public sealed record SwitchAddOnFile(string Path, string TitleId, IReadOnlyList<string> ContentNcas, int? Version);
