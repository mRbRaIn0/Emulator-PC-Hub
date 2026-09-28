using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Cemu;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.Handheld;
using EmulatorPCHub.Emulation.Switch;
using EmulatorPCHub.Emulation.WheelWizard;
using EmulatorPCHub.Emulation.WiiCompiled;

namespace EmulatorPCHub.Emulation;

/// <summary>Alle verfügbaren Adapter. Der Switch-Adapter wird über die Konfiguration gewählt.</summary>
public sealed class AdapterRegistry
{
    private readonly Dictionary<string, Func<SwitchEmulatorAdapter>> _switchFactories;
    private readonly ConfigService _config;

    public DolphinAdapter Dolphin { get; }
    public CemuAdapter Cemu { get; }
    public WiiCompiledAdapter WiiCompiled { get; }
    public MelonDsAdapter MelonDS { get; }
    public AzaharAdapter Azahar { get; }
    public WheelWizardIntegration WheelWizard { get; }
    public SwitchEmulatorAdapter Switch => _switchFactories.TryGetValue(_config.Current.Emulators.SwitchAdapter, out var f)
        ? f()
        : _switchFactories["eden"]();

    public AdapterRegistry(AppPaths paths, ConfigService config, BackupService backups)
    {
        _config = config;
        WheelWizard = new WheelWizardIntegration(paths, config, backups);
        Dolphin = new DolphinAdapter(paths, config, WheelWizard);
        Cemu = new CemuAdapter(paths, config);
        WiiCompiled = new WiiCompiledAdapter(paths, config, WheelWizard);
        MelonDS = new MelonDsAdapter(paths, config);
        Azahar = new AzaharAdapter(paths, config);
        var eden = new EdenAdapter(paths, config, backups);
        _switchFactories = new Dictionary<string, Func<SwitchEmulatorAdapter>>(StringComparer.OrdinalIgnoreCase)
        {
            ["eden"] = () => eden,
        };
        if (!_switchFactories.ContainsKey(config.Current.Emulators.SwitchAdapter))
            config.Update(c => c.Emulators.SwitchAdapter = "eden");
    }

    /// <summary>Weitere Switch-Emulatoren können hier registriert werden, ohne die App umzubauen.</summary>
    public void RegisterSwitchAdapter(string id, Func<SwitchEmulatorAdapter> factory) => _switchFactories[id] = factory;

    public IReadOnlyList<string> SwitchAdapterIds => _switchFactories.Keys.ToList();

    public IEmulatorAdapter? Get(string id) => id switch
    {
        EmulatorIds.Dolphin => Dolphin,
        EmulatorIds.Cemu => Cemu,
        EmulatorIds.Switch => Switch,
        EmulatorIds.WiiCompiled => WiiCompiled,
        EmulatorIds.MelonDS => MelonDS,
        EmulatorIds.Azahar => Azahar,
        _ => null,
    };

    public IEnumerable<IEmulatorAdapter> All => [Dolphin, Cemu, Switch, WiiCompiled, MelonDS, Azahar];

    public IEmulatorAdapter? ForGame(GameEntry game) => Get(game.EmulatorId) ?? Get(game.Platform.DefaultAdapterId());
}
