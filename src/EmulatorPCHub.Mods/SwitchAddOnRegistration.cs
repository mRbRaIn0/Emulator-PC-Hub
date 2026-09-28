using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Library;

namespace EmulatorPCHub.Mods;

/// <summary>Trägt eigene Updates/DLCs eines Switch-Spiels beim Switch-Emulator ein (für alle Spiele, nicht nur MK8DX).</summary>
public static class SwitchAddOnRegistration
{
    /// <returns>Anzahl eingetragener Dateien.</returns>
    public static int Register(SwitchEmulatorAdapter adapter, string titleId, SwitchAddOns addOns)
    {
        var updates = addOns.Updates.Select(u => new SwitchAddOnFile(u.Path, u.TitleId ?? "", [], u.Version)).ToList();
        var dlcs = addOns.Dlcs
            .Where(d => d.TitleId != null)
            .Select(d => new SwitchAddOnFile(d.Path, d.TitleId!,
                d.NcaFiles.Where(n => !n.Contains(".cnmt.", StringComparison.OrdinalIgnoreCase)).ToList(), d.Version))
            .ToList();
        adapter.RegisterAddOns(titleId, updates, dlcs);
        return updates.Count + dlcs.Count;
    }

    /// <summary>Alle gefundenen Updates/DLCs der Bibliothek eintragen.</summary>
    public static int RegisterAll(SwitchEmulatorAdapter adapter, LibraryService library) =>
        library.SwitchAddOns.Sum(pair => Register(adapter, pair.Key, pair.Value));
}
