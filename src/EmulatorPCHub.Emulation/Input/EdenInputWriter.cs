using System.Globalization;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Eden: schreibt die Spieler des Profils in den Abschnitt <c>[Controls]</c> von <c>qt-config.ini</c>
/// (<c>player_N_connected</c>, <c>player_N_type</c>, <c>player_N_button_a</c> …). Die Belegung entspricht Edens
/// eigener Auto-Belegung (SDLDriver::GetButtonMappingForDevice): Tasten nach Position (Switch-A = rechte Taste),
/// Geräte-ID = SDL-GUID mit genullter CRC, Port = Index bei gleichen Geräten.
/// Tastatur-Spieler bekommen Edens Standard-Tastaturbelegung.
/// </summary>
public sealed class EdenInputWriter : IInputConfigWriter
{
    private const string Section = "Controls";
    private const int MaxPlayers = 8;

    // Settings::NativeButton::mapping → SDL-Mapping-Name (a = unten, b = rechts, x = links, y = oben)
    private static readonly (string Eden, string Sdl)[] Buttons =
    [
        ("button_a", "b"), ("button_b", "a"), ("button_x", "y"), ("button_y", "x"),
        ("button_lstick", "leftstick"), ("button_rstick", "rightstick"),
        ("button_l", "leftshoulder"), ("button_r", "rightshoulder"),
        ("button_zl", "lefttrigger"), ("button_zr", "righttrigger"),
        ("button_plus", "start"), ("button_minus", "back"),
        ("button_dleft", "dpleft"), ("button_dup", "dpup"), ("button_dright", "dpright"), ("button_ddown", "dpdown"),
        ("button_slleft", "leftshoulder"), ("button_srleft", "rightshoulder"),
        ("button_home", "guide"), ("button_screenshot", "misc1"),
        ("button_slright", "leftshoulder"), ("button_srright", "rightshoulder"),
    ];

    private static readonly string[] Analogs = ["lstick", "rstick"];
    private static readonly string[] Motions = ["motionleft", "motionright"];

    private readonly EdenAdapter _eden;

    public EdenInputWriter(EdenAdapter eden)
    {
        _eden = eden;
    }

    public string Apply(InputSetup setup, GameEntry game)
    {
        var notes = new List<string>();
        _eden.EditConfig(ini =>
        {
            Write(ini, setup, notes);
            return true;
        });
        var text = $"Eden: {setup.Players.Count} Spieler konfiguriert";
        return notes.Count == 0 ? text : text + " (" + string.Join("; ", notes) + ")";
    }

    /// <summary>Überträgt die Spieler in eine geladene qt-config.ini (öffentlich für Tests).</summary>
    public static void Write(EdenIni ini, InputSetup setup, List<string>? notes = null)
    {
        for (var i = 0; i < MaxPlayers; i++)
        {
            var player = setup.Players.FirstOrDefault(p => p.Player == i + 1);
            var prefix = $"player_{i}_";
            ini.Set(Section, prefix + "connected", player != null);
            if (player == null)
                continue;

            ini.SetRaw(Section, prefix + @"profile_name\default", "true");
            ini.Remove(Section, prefix + "profile_name");
            ini.Set(Section, prefix + "type", player.Pad == EmulatedPad.SwitchJoyConPair ? 1 : 0); // 0 = Pro Controller, 1 = Joy-Con-Paar
            ini.Set(Section, prefix + "vibration_enabled", setup.Rumble);

            if (!setup.ManageMappings)
                continue;
            var d = player.Device;
            if (d.IsKeyboard)
            {
                foreach (var key in Buttons.Select(b => b.Eden).Concat(Analogs).Concat(Motions))
                    ini.ResetToDefault(Section, prefix + key);
                continue;
            }
            var mapping = ParseMapping(d.SdlMapping);
            if (d.SdlGuid == null || mapping.Count == 0)
            {
                notes?.Add($"P{player.Player}: {d.Name} ohne SDL-Belegung – in Eden selbst zuordnen");
                continue;
            }
            var guid = EdenGuid(d.SdlGuid);
            var port = d.GuidIndex;
            foreach (var (eden, sdl) in WithRemap(Buttons, ButtonMap.Overrides(player.Pad, d.NintendoLayout, setup.Remap)))
            {
                if (mapping.TryGetValue(sdl, out var bind) && ButtonParam(bind, port, guid) is { } param)
                    ini.Set(Section, prefix + eden, param);
                else
                    ini.Set(Section, prefix + eden, "[empty]");
            }
            ini.Set(Section, prefix + "lstick", StickParam(mapping, "leftx", "lefty", port, guid) ?? "[empty]");
            ini.Set(Section, prefix + "rstick", StickParam(mapping, "rightx", "righty", port, guid) ?? "[empty]");
            var motion = d.Kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4 or ControllerKind.SwitchPro or ControllerKind.JoyCon
                ? $"engine:sdl,motion:0,port:{port},guid:{guid}"
                : "[empty]";
            foreach (var m in Motions)
                ini.Set(Section, prefix + m, motion);
        }
    }

    // Konsolen-Taste (Zielname aus ButtonMap) → Eden-Schlüssel
    private static readonly Dictionary<string, string> TargetKeys = new()
    {
        ["A"] = "button_a", ["B"] = "button_b", ["X"] = "button_x", ["Y"] = "button_y",
        ["L"] = "button_l", ["R"] = "button_r", ["ZL"] = "button_zl", ["ZR"] = "button_zr",
        ["+"] = "button_plus", ["−"] = "button_minus", ["Home"] = "button_home",
        ["Linken Stick drücken"] = "button_lstick", ["Rechten Stick drücken"] = "button_rstick",
        ["Steuerkreuz ↑"] = "button_dup", ["Steuerkreuz ↓"] = "button_ddown", ["Steuerkreuz ←"] = "button_dleft", ["Steuerkreuz →"] = "button_dright",
    };

    /// <summary>Eigene Umbelegung: Eden-Schlüssel bekommt die SDL-Taste der gewählten Gamepad-Taste (leer = nicht belegt).</summary>
    private static IEnumerable<(string Eden, string Sdl)> WithRemap((string Eden, string Sdl)[] buttons, IReadOnlyList<ButtonRow> overrides)
    {
        var result = buttons.ToList();
        foreach (var row in overrides)
        {
            if (!TargetKeys.TryGetValue(row.Target, out var key))
                continue;
            var i = result.FindIndex(b => b.Eden == key);
            if (i >= 0)
                result[i] = (key, row.Source is { } src ? ButtonMap.SdlName(src) : "");
        }
        return result;
    }

    /// <summary>Eden-Geräte-ID: SDL-GUID als Hex mit genullter Namens-CRC (Bytes 2–3).</summary>
    public static string EdenGuid(string sdlGuidHex)
    {
        var s = sdlGuidHex.ToLowerInvariant().PadRight(32, '0');
        return s[..4] + "0000" + s[8..32];
    }

    /// <summary>„guid,name,a:b0,leftx:a0,dpup:h0.1,…“ → Name → Rohbelegung.</summary>
    public static Dictionary<string, string> ParseMapping(string? mapping)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(mapping))
            return result;
        var parts = mapping.Split(',');
        foreach (var part in parts.Skip(2))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0)
                continue;
            var key = part[..colon].Trim().TrimStart('+', '-');
            result.TryAdd(key, part[(colon + 1)..].Trim());
        }
        return result;
    }

    private static string? ButtonParam(string bind, int port, string guid)
    {
        var b = bind.TrimEnd('~');
        var sign = "+";
        if (b.StartsWith('+') || b.StartsWith('-'))
        {
            sign = b[..1];
            b = b[1..];
        }
        if (b.StartsWith('b') && int.TryParse(b[1..], out var button))
            return $"engine:sdl,port:{port},guid:{guid},button:{button}";
        if (b.StartsWith('a') && int.TryParse(b[1..], out var axis))
            return $"engine:sdl,port:{port},guid:{guid},axis:{axis},threshold:0.5,invert:{sign}";
        if (b.StartsWith('h'))
        {
            var dot = b.IndexOf('.');
            if (dot > 1 && int.TryParse(b[1..dot], out var hat) && int.TryParse(b[(dot + 1)..], out var mask))
            {
                var dir = mask switch { 1 => "up", 2 => "right", 4 => "down", 8 => "left", _ => null };
                if (dir != null)
                    return $"engine:sdl,port:{port},guid:{guid},hat:{hat},direction:{dir}";
            }
        }
        return null;
    }

    private static string? StickParam(Dictionary<string, string> mapping, string xName, string yName, int port, string guid)
    {
        if (!mapping.TryGetValue(xName, out var x) || !mapping.TryGetValue(yName, out var y))
            return null;
        static int? Axis(string v)
        {
            var s = v.TrimStart('+', '-').TrimEnd('~');
            return s.StartsWith('a') && int.TryParse(s[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        }
        var ax = Axis(x);
        var ay = Axis(y);
        if (ax == null || ay == null)
            return null;
        var ix = x.EndsWith('~') ? "-" : "+";
        var iy = y.EndsWith('~') ? "-" : "+";
        return $"engine:sdl,port:{port},guid:{guid},axis_x:{ax},axis_y:{ay},offset_x:0.000000,offset_y:0.000000,invert_x:{ix},invert_y:{iy}";
    }
}
