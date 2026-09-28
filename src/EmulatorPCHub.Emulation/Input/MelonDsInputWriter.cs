using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Handheld;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// melonDS: schreibt Spieler 1 in <c>melonDS.toml</c> (<c>[Instance0]</c> JoystickID, <c>[Instance0.Joystick]</c>).
/// melonDS liest den Controller über die rohe SDL-Joystick-API; die Werte folgen EmuInstance::joystickButtonDown:
/// Taste = Index, Hat = <c>0x100 | hat&lt;&lt;4 | Richtung</c>, Achse = <c>0x10000 | achse&lt;&lt;24 | modus&lt;&lt;20</c>
/// (0 = positiv, 1 = negativ, 2 = Trigger) – Taste und Achse lassen sich in einem Wert kombinieren.
/// Die Indizes stammen aus dem SDL-Mapping des Geräts (dieselbe Rohnummerierung).
/// Fehlt eine Tastaturbelegung (melonDS 1.1 startet mit -1), setzt der Hub eine Standardbelegung.
/// </summary>
public sealed class MelonDsInputWriter : IInputConfigWriter
{
    private const string Instance = "Instance0";
    private const string JoyTable = "Instance0.Joystick";
    private const string KeyTable = "Instance0.Keyboard";
    private const int NoButton = 0xFFFF;

    // melonDS-Taste → Zielname in ButtonMap (EmulatedPad.NintendoDS)
    private static readonly (string Melon, string Target)[] Buttons =
    [
        ("A", "A"), ("B", "B"), ("X", "X"), ("Y", "Y"), ("L", "L"), ("R", "R"),
        ("Start", "Start"), ("Select", "Select"),
        ("Up", "Steuerkreuz ↑"), ("Down", "Steuerkreuz ↓"), ("Left", "Steuerkreuz ←"), ("Right", "Steuerkreuz →"),
    ];

    // Qt::Key-Codes: A=X, B=Z, X=S, Y=A, L=Q, R=W, Start=Enter, Select=Rücktaste, Steuerkreuz = Pfeiltasten
    private static readonly (string Melon, int Key)[] KeyboardDefaults =
    [
        ("A", 0x58), ("B", 0x5A), ("X", 0x53), ("Y", 0x41), ("L", 0x51), ("R", 0x57),
        ("Start", 0x01000004), ("Select", 0x01000003),
        ("Up", 0x01000013), ("Down", 0x01000015), ("Left", 0x01000012), ("Right", 0x01000014),
    ];

    private readonly string _configFile;
    private readonly BackupService? _backups;

    public MelonDsInputWriter(string configFile, BackupService? backups)
    {
        _configFile = configFile;
        _backups = backups;
    }

    public string Apply(InputSetup setup, GameEntry game)
    {
        if (File.Exists(_configFile) && !File.Exists(_configFile + ".hub-original"))
        {
            File.Copy(_configFile, _configFile + ".hub-original");
            _backups?.BackupFile(BackupCategory.Config, _configFile, "melonds-config");
        }
        var toml = MelonToml.Load(_configFile);
        var text = Write(toml, setup);
        toml.Save(_configFile);
        return text;
    }

    /// <summary>Überträgt Spieler 1 in eine geladene melonDS.toml (öffentlich für Tests).</summary>
    public static string Write(MelonToml toml, InputSetup setup)
    {
        foreach (var (melon, key) in KeyboardDefaults)
            if ((toml.GetInt(KeyTable, melon) ?? -1) == -1)
                toml.Set(KeyTable, melon, key);

        var player = setup.Players.OrderBy(p => p.Player).FirstOrDefault();
        var extra = setup.Players.Count > 1 ? " (melonDS steuert nur Spieler 1)" : "";
        if (player == null)
            return "melonDS: kein Spieler zugeordnet – Tastatur aktiv";
        var d = player.Device;
        if (d.IsKeyboard)
            return "melonDS: Spieler 1 = Tastatur" + extra;
        if (!setup.ManageMappings)
            return $"melonDS: Spieler 1 = {d.Name} (Belegung im Emulator)" + extra;

        var mapping = EdenInputWriter.ParseMapping(d.SdlMapping);
        if (mapping.Count == 0)
            return $"melonDS: {d.Name} ohne SDL-Belegung – in melonDS selbst zuordnen" + extra;

        toml.Set(Instance, "JoystickID", d.SdlIndex);
        var rows = ButtonMap.For(EmulatedPad.NintendoDS, d.NintendoLayout, setup.Remap);
        var stickX = AxisOf(mapping, "leftx");
        var stickY = AxisOf(mapping, "lefty");
        foreach (var (melon, target) in Buttons)
        {
            var source = rows.FirstOrDefault(r => r.Target == target)?.Source;
            var value = source is { } src && mapping.TryGetValue(ButtonMap.SdlName(src), out var bind) ? ButtonValue(bind) : NoButton;
            // Steuerkreuz zusätzlich über den linken Stick
            var axis = melon switch
            {
                "Right" when stickX != null => AxisValue(stickX.Value.axis, stickX.Value.inverted ? 1 : 0),
                "Left" when stickX != null => AxisValue(stickX.Value.axis, stickX.Value.inverted ? 0 : 1),
                "Down" when stickY != null => AxisValue(stickY.Value.axis, stickY.Value.inverted ? 1 : 0),
                "Up" when stickY != null => AxisValue(stickY.Value.axis, stickY.Value.inverted ? 0 : 1),
                _ => 0,
            };
            if (value == NoButton && axis == 0)
                toml.Set(JoyTable, melon, -1);
            else
                toml.Set(JoyTable, melon, (value & 0xFFFF) | axis | (value & ~0xFFFF));
        }
        return $"melonDS: Spieler 1 = {d.Name} als Nintendo DS" + extra;
    }

    /// <summary>SDL-Mapping-Wert („b3“, „h0.4“, „a4“, „+a1“, „-a0“) → melonDS-Wert (ohne Stick-Anteil: untere 16 Bit = 0xFFFF).</summary>
    public static int ButtonValue(string bind)
    {
        var b = bind.TrimEnd('~');
        var half = b.StartsWith('+') ? 0 : b.StartsWith('-') ? 1 : -1;
        b = b.TrimStart('+', '-');
        if (b.StartsWith('b') && int.TryParse(b[1..], out var button))
            return button & 0xFFFF;
        if (b.StartsWith('h'))
        {
            var dot = b.IndexOf('.');
            if (dot > 1 && int.TryParse(b[1..dot], out var hat) && int.TryParse(b[(dot + 1)..], out var mask))
                return 0x100 | ((hat & 0xF) << 4) | (mask & 0xF);
        }
        if (b.StartsWith('a') && int.TryParse(b[1..], out var axis))
            return NoButton | AxisValue(axis, half < 0 ? 2 : half);
        return NoButton;
    }

    private static int AxisValue(int axis, int mode) => 0x10000 | ((axis & 0xF) << 24) | ((mode & 0xF) << 20);

    private static (int axis, bool inverted)? AxisOf(Dictionary<string, string> mapping, string name)
    {
        if (!mapping.TryGetValue(name, out var v))
            return null;
        var s = v.TrimStart('+', '-').TrimEnd('~');
        return s.StartsWith('a') && int.TryParse(s[1..], out var n) ? (n, v.EndsWith('~')) : null;
    }
}
