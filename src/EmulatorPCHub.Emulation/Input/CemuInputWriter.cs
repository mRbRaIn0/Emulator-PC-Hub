using System.Xml.Linq;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Cemu: schreibt <c>controllerProfiles/controllerN.xml</c> (emulierter Wii U Pro Controller).
/// Xbox → XInput-API, PS/Nintendo → SDLController-API (UUID „index_GUID“), Tastatur → Keyboard-API.
/// Mapping-Werte entsprechen Cemus eigenen Standardbelegungen (ProController.cpp).
/// </summary>
public sealed class CemuInputWriter : IInputConfigWriter
{
    private readonly string _dataDir;
    private readonly BackupService? _backups;

    // ProController::ButtonId
    private const int A = 1, B = 2, X = 3, Y = 4, L = 5, R = 6, ZL = 7, ZR = 8, Plus = 9, Minus = 10, Home = 11;
    private const int Up = 12, Down = 13, Left = 14, Right = 15, StickL = 16, StickR = 17;
    private const int LUp = 18, LDown = 19, LLeft = 20, LRight = 21, RUp = 22, RDown = 23, RLeft = 24, RRight = 25;

    // Buttons2 (Controller.h)
    private const int AxisXP = 38, AxisYP = 39, RotXP = 40, RotYP = 41, TrigXP = 42, TrigYP = 43;
    private const int AxisXN = 44, AxisYN = 45, RotXN = 46, RotYN = 47;

    public CemuInputWriter(string cemuDataDir, BackupService? backups)
    {
        _dataDir = cemuDataDir;
        _backups = backups;
    }

    public string Apply(InputSetup setup, GameEntry game)
    {
        var dir = Path.Combine(_dataDir, "controllerProfiles");
        Directory.CreateDirectory(dir);
        foreach (var player in setup.Players)
        {
            var file = Path.Combine(dir, $"controller{player.Player - 1}.xml");
            if (File.Exists(file) && !File.Exists(file + ".hub-original"))
            {
                File.Copy(file, file + ".hub-original");
                _backups?.BackupFile(BackupCategory.Config, file, $"cemu-controller{player.Player - 1}");
            }
            BuildProfile(player, setup.Rumble, setup.Remap).Save(file);
        }
        // Nicht belegte Spieler entfernen, damit keine alten Geräte hängen bleiben
        for (int p = 1; p <= 4; p++)
        {
            if (setup.Players.All(x => x.Player != p))
            {
                var file = Path.Combine(dir, $"controller{p - 1}.xml");
                if (File.Exists(file) && File.Exists(file + ".hub-original"))
                    File.Delete(file);
            }
        }
        return $"Cemu: {setup.Players.Count} Spieler als Wii U Pro Controller";
    }

    public static XDocument BuildProfile(ResolvedPlayer player, bool rumble, IReadOnlyList<ButtonRemap>? remap = null)
    {
        var d = player.Device;
        string api, uuid;
        IEnumerable<(int mapping, int button)> mapping;
        if (d.IsKeyboard)
        {
            api = "Keyboard";
            uuid = "keyboard";
            mapping = KeyboardMapping();
        }
        else if (d.Kind == ControllerKind.Xbox && d.XInputIndex is { } xi)
        {
            api = "XInput";
            uuid = xi.ToString();
            mapping = XInputMapping();
        }
        else
        {
            api = "SDLController";
            uuid = $"{d.GuidIndex}_{d.SdlGuid}";
            mapping = SdlMapping();
        }

        if (!d.IsKeyboard && remap != null)
            mapping = WithRemap(mapping.ToList(), ButtonMap.Overrides(player.Pad, d.NintendoLayout, remap), api == "XInput");

        var controller = new XElement("controller",
            new XElement("api", api),
            new XElement("uuid", uuid),
            new XElement("display_name", d.Name),
            new XElement("rumble", rumble && !d.IsKeyboard ? "0.5" : "0"),
            new XElement("axis", new XElement("deadzone", "0.25"), new XElement("range", "1")),
            new XElement("rotation", new XElement("deadzone", "0.25"), new XElement("range", "1")),
            new XElement("trigger", new XElement("deadzone", "0.25"), new XElement("range", "1")),
            new XElement("mappings", mapping.Select(m =>
                new XElement("entry", new XElement("mapping", m.mapping), new XElement("button", m.button)))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("emulated_controller",
                new XElement("type", "Wii U Pro Controller"),
                controller));
    }

    // Konsolen-Taste (Zielname aus ButtonMap) → Wii-U-Pro-Mapping-ID
    private static readonly Dictionary<string, int> TargetIds = new()
    {
        ["A"] = A, ["B"] = B, ["X"] = X, ["Y"] = Y, ["L"] = L, ["R"] = R, ["ZL"] = ZL, ["ZR"] = ZR,
        ["+"] = Plus, ["−"] = Minus, ["Home"] = Home,
        ["Steuerkreuz ↑"] = Up, ["Steuerkreuz ↓"] = Down, ["Steuerkreuz ←"] = Left, ["Steuerkreuz →"] = Right,
        ["Linken Stick drücken"] = StickL, ["Rechten Stick drücken"] = StickR,
    };

    /// <summary>Cemu-Tastencode einer Gamepad-Taste (SDLController- bzw. XInput-API); null = nicht verfügbar.</summary>
    private static int? SourceCode(PadButton b, bool xinput) => xinput
        ? b switch
        {
            PadButton.South => 12, PadButton.East => 13, PadButton.West => 14, PadButton.North => 15,
            PadButton.L1 => 8, PadButton.R1 => 9, PadButton.L2 => TrigXP, PadButton.R2 => TrigYP,
            PadButton.Start => 4, PadButton.Back => 5, PadButton.L3 => 6, PadButton.R3 => 7,
            PadButton.DUp => 0, PadButton.DDown => 1, PadButton.DLeft => 2, PadButton.DRight => 3,
            _ => null,
        }
        : b switch
        {
            PadButton.South => 0, PadButton.East => 1, PadButton.West => 2, PadButton.North => 3,
            PadButton.Back => 4, PadButton.Guide => 5, PadButton.Start => 6, PadButton.L3 => 7, PadButton.R3 => 8,
            PadButton.L1 => 9, PadButton.R1 => 10, PadButton.L2 => TrigXP, PadButton.R2 => TrigYP,
            PadButton.DUp => 11, PadButton.DDown => 12, PadButton.DLeft => 13, PadButton.DRight => 14,
            _ => null,
        };

    private static List<(int, int)> WithRemap(List<(int mapping, int button)> mapping, IReadOnlyList<ButtonRow> overrides, bool xinput)
    {
        foreach (var row in overrides)
        {
            if (!TargetIds.TryGetValue(row.Target, out var id))
                continue;
            mapping.RemoveAll(m => m.mapping == id);
            if (row.Source is { } src && SourceCode(src, xinput) is { } code)
                mapping.Add((id, code));
        }
        return mapping;
    }

    private static IEnumerable<(int, int)> XInputMapping() =>
    [
        (A, 13), (B, 12), (X, 15), (Y, 14), (L, 8), (R, 9), (ZL, TrigXP), (ZR, TrigYP),
        (Plus, 4), (Minus, 5), (Up, 0), (Down, 1), (Left, 2), (Right, 3), (StickL, 6), (StickR, 7),
        (LUp, AxisYP), (LDown, AxisYN), (LLeft, AxisXN), (LRight, AxisXP),
        (RUp, RotYP), (RDown, RotYN), (RLeft, RotXN), (RRight, RotXP),
    ];

    private static IEnumerable<(int, int)> SdlMapping() =>
    [
        (A, 1), (B, 0), (X, 3), (Y, 2), (L, 9), (R, 10), (ZL, TrigXP), (ZR, TrigYP),
        (Plus, 6), (Minus, 4), (Home, 5), (Up, 11), (Down, 12), (Left, 13), (Right, 14), (StickL, 7), (StickR, 8),
        (LUp, AxisYN), (LDown, AxisYP), (LLeft, AxisXN), (LRight, AxisXP),
        (RUp, RotYN), (RDown, RotYP), (RLeft, RotXN), (RRight, RotXP),
    ];

    /// <summary>Tastatur (Windows-Tastencodes): A=Z, B=X, X=C, Y=V, Stick WASD, rechter Stick IJKL, D-Pad Pfeile.</summary>
    private static IEnumerable<(int, int)> KeyboardMapping() =>
    [
        (A, 0x5A), (B, 0x58), (X, 0x43), (Y, 0x56), (L, 0x45), (R, 0x55), (ZL, 0x51), (ZR, 0x4F),
        (Plus, 0x0D), (Minus, 0xBD), (Up, 0x26), (Down, 0x28), (Left, 0x25), (Right, 0x27), (StickL, 0x46), (StickR, 0x48),
        (LUp, 0x57), (LDown, 0x53), (LLeft, 0x41), (LRight, 0x44),
        (RUp, 0x49), (RDown, 0x4B), (RLeft, 0x4A), (RRight, 0x4C),
    ];
}
