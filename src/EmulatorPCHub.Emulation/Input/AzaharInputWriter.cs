using System.Globalization;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Azahar (3DS): schreibt Spieler 1 in das aktive Profil von <c>qt-config.ini</c>
/// (<c>[Controls]</c> → <c>profiles\N\button_a</c> …). Werte wie Azahars eigene Auto-Belegung
/// (SDLState::GetSDLControllerButtonBind): <c>api:controller</c> mit SDL-GameController-Nummern, d. h. nach Position
/// unabhängig vom Gerätetyp. <c>maptype:all</c> lässt Azahar jeden verbundenen Controller annehmen – so spielt eine
/// abweichende GUID-Schreibweise (SDL2 in Azahar, SDL3 im Hub) keine Rolle.
/// Tastatur-Spieler bekommen Azahars Standard-Tastaturbelegung.
/// </summary>
public sealed class AzaharInputWriter : IInputConfigWriter
{
    private const string Section = "Controls";

    // Azahar-Schlüssel → Zielname in ButtonMap (EmulatedPad.Nintendo3DS)
    private static readonly (string Key, string Target)[] Buttons =
    [
        ("button_a", "A"), ("button_b", "B"), ("button_x", "X"), ("button_y", "Y"),
        ("button_l", "L"), ("button_r", "R"), ("button_zl", "ZL"), ("button_zr", "ZR"),
        ("button_start", "Start"), ("button_select", "Select"), ("button_home", "Home"),
        ("button_up", "Steuerkreuz ↑"), ("button_down", "Steuerkreuz ↓"),
        ("button_left", "Steuerkreuz ←"), ("button_right", "Steuerkreuz →"),
    ];

    private static readonly string[] Analogs = ["circle_pad", "c_stick"];

    private readonly string _configFile;
    private readonly BackupService? _backups;

    public AzaharInputWriter(string configFile, BackupService? backups)
    {
        _configFile = configFile;
        _backups = backups;
    }

    public string Apply(InputSetup setup, GameEntry game)
    {
        if (File.Exists(_configFile) && !File.Exists(_configFile + ".hub-original"))
        {
            File.Copy(_configFile, _configFile + ".hub-original");
            _backups?.BackupFile(BackupCategory.Config, _configFile, "azahar-qt-config");
        }
        var ini = EdenIni.Load(_configFile);
        var text = Write(ini, setup);
        ini.Save(_configFile);
        return text;
    }

    /// <summary>Überträgt Spieler 1 in eine geladene qt-config.ini (öffentlich für Tests).</summary>
    public static string Write(EdenIni ini, InputSetup setup)
    {
        var index = int.TryParse(ini.Get(Section, "profile"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p + 1 : 1;
        var prefix = $@"profiles\{index}\";
        var player = setup.Players.OrderBy(x => x.Player).FirstOrDefault();
        var extra = setup.Players.Count > 1 ? " (der 3DS hat nur Spieler 1)" : "";
        if (player == null || !setup.ManageMappings)
            return player == null ? "Azahar: kein Spieler zugeordnet" : $"Azahar: Spieler 1 = {player.Device.Name} (Belegung im Emulator)";

        var d = player.Device;
        if (d.IsKeyboard)
        {
            foreach (var key in Buttons.Select(b => b.Key).Concat(Analogs))
                ini.ResetToDefault(Section, prefix + key);
            return "Azahar: Spieler 1 = Tastatur (Standardbelegung)" + extra;
        }

        var guid = (d.SdlGuid ?? "0").ToLowerInvariant();
        var device = $"engine:sdl,api:controller,maptype:all,guid:{guid},port:{d.GuidIndex}";
        var rows = ButtonMap.For(EmulatedPad.Nintendo3DS, d.NintendoLayout, setup.Remap);
        foreach (var (key, target) in Buttons)
        {
            var source = rows.FirstOrDefault(r => r.Target == target)?.Source;
            ini.Set(Section, prefix + key, source is { } src ? $"{device},{Bind(src)}" : "");
        }
        ini.Set(Section, prefix + "circle_pad", $"{device},axis_x:0,axis_y:1,deadzone:0.100000");
        ini.Set(Section, prefix + "c_stick", $"{device},axis_x:2,axis_y:3,deadzone:0.100000");
        return $"Azahar: Spieler 1 = {d.Name} als Nintendo 3DS" + extra;
    }

    /// <summary>Gamepad-Taste → SDL-GameController-Taste bzw. -Achse (Trigger).</summary>
    public static string Bind(PadButton b) => b switch
    {
        PadButton.L2 => "axis:4,direction:+,threshold:0.500000",
        PadButton.R2 => "axis:5,direction:+,threshold:0.500000",
        _ => "button:" + (b switch
        {
            PadButton.South => 0,
            PadButton.East => 1,
            PadButton.West => 2,
            PadButton.North => 3,
            PadButton.Back => 4,
            PadButton.Guide => 5,
            PadButton.Start => 6,
            PadButton.L3 => 7,
            PadButton.R3 => 8,
            PadButton.L1 => 9,
            PadButton.R1 => 10,
            PadButton.DUp => 11,
            PadButton.DDown => 12,
            PadButton.DLeft => 13,
            PadButton.DRight => 14,
            _ => -1,
        }).ToString(CultureInfo.InvariantCulture),
    };
}
