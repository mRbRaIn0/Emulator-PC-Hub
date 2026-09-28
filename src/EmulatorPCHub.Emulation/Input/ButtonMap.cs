using EmulatorPCHub.Core.Input;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Eine Konsolen-Taste und die Gamepad-Taste, die sie auslöst (null = nicht belegt).
/// <see cref="DefaultSource"/> ist die Standardbelegung des Hubs.
/// </summary>
public sealed record ButtonRow(string Target, PadButton? Source, PadButton DefaultSource)
{
    public bool Changed => Source != DefaultSource;
    /// <summary>Sticks (Achsen) lassen sich nicht auf Tasten umlegen.</summary>
    public bool Fixed => ButtonMap.IsStick(DefaultSource);
}

/// <summary>
/// Lesbare Belegung „Gamepad-Taste → Konsolen-Taste“, genau so, wie sie die Input-Writer in die Emulatoren schreiben
/// (<see cref="DolphinInputWriter"/>, <see cref="CemuInputWriter"/>, <see cref="EdenInputWriter"/>).
/// Die Zielnamen sind zugleich die Schlüssel für die Funktionen im Spiel.
/// </summary>
public static class ButtonMap
{
    public static readonly PadButton[] AllButtons = Enum.GetValues<PadButton>();

    /// <summary>Gamepad-Tasten, die man frei zuweisen kann (alles außer den Sticks).</summary>
    public static readonly PadButton[] Assignable = AllButtons.Where(b => !IsStick(b)).ToArray();

    public static bool IsStick(PadButton b) => b is PadButton.LeftStick or PadButton.RightStick;

    /// <summary>Belegung je Konsolen-Taste (Standard + eigene Umbelegung), sortiert nach der Standard-Gamepad-Taste.</summary>
    public static IReadOnlyList<ButtonRow> For(EmulatedPad pad, bool nintendoLayout, IEnumerable<ButtonRemap>? remap = null)
    {
        var rows = Defaults(pad, nintendoLayout)
            .OrderBy(kv => kv.Key)
            .Select(kv => new ButtonRow(kv.Value, kv.Key, kv.Key))
            .ToList();
        foreach (var r in remap ?? [])
        {
            if (r.Pad != pad || (r.LabelBased && nintendoLayout))
                continue;
            var i = rows.FindIndex(x => x.Target == r.Target);
            if (i >= 0 && !rows[i].Fixed && (r.Source == null || !IsStick(r.Source.Value)))
                rows[i] = rows[i] with { Source = r.Source };
        }
        return rows;
    }

    /// <summary>Gamepad-Tasten, die im Spiel nichts auslösen.</summary>
    public static IReadOnlyList<PadButton> Unused(IReadOnlyList<ButtonRow> rows) =>
        AllButtons.Where(b => rows.All(r => r.Source != b)).ToList();

    /// <summary>Nur die geänderten Tasten – das, was die Input-Writer zusätzlich zur Standardbelegung schreiben.</summary>
    public static IReadOnlyList<ButtonRow> Overrides(EmulatedPad pad, bool nintendoLayout, IEnumerable<ButtonRemap>? remap) =>
        remap == null ? [] : For(pad, nintendoLayout, remap).Where(r => r.Changed).ToList();

    /// <summary>
    /// Umbelegung speichern: <paramref name="target"/> auf <paramref name="source"/> legen. Liegt dort schon eine andere
    /// Konsolen-Taste, bekommt sie die bisherige Gamepad-Taste (Tausch). Einträge gleich der Standardbelegung entfallen.
    /// </summary>
    public static void Assign(List<ButtonRemap> remap, EmulatedPad pad, bool nintendoLayout, string target, PadButton? source)
    {
        var rows = For(pad, nintendoLayout, remap);
        var row = rows.FirstOrDefault(r => r.Target == target);
        if (row == null || row.Fixed || row.Source == source)
            return;
        var other = source == null ? null : rows.FirstOrDefault(r => r.Source == source && r.Target != target);
        if (other != null)
        {
            if (other.Fixed)
                return; // auf einer Stick-Achse kann keine Taste liegen
            Set(remap, pad, other, row.Source);
        }
        Set(remap, pad, row, source);
    }

    /// <summary>Controller, die der Hub nach Position belegt (rechte Taste = A) – hier greift „Bestätigen mit ✕“.</summary>
    public static readonly EmulatedPad[] PositionalPads =
        [EmulatedPad.WiiUPro, EmulatedPad.WiiUGamePad, EmulatedPad.SwitchPro, EmulatedPad.SwitchJoyConPair, EmulatedPad.NintendoDS, EmulatedPad.Nintendo3DS];

    /// <summary>
    /// „Bestätigen mit ✕“: legt auf PlayStation-/Xbox-Controllern A auf die untere und B auf die rechte Taste
    /// (wie in PlayStation-/Xbox-Spielen) – für Wii U, Switch, DS und 3DS. Eigene Umbelegungen von A/B bleiben unangetastet.
    /// </summary>
    public static void ApplyConfirmSouth(List<ButtonRemap> remap, bool enabled)
    {
        remap.RemoveAll(r => r.LabelBased);
        if (!enabled)
            return;
        foreach (var pad in PositionalPads)
        {
            if (remap.Any(r => r.Pad == pad && (r.Target is "A" or "B" || r.Source is PadButton.South or PadButton.East)))
                continue;
            remap.Add(new ButtonRemap { Pad = pad, Target = "A", Source = PadButton.South, LabelBased = true });
            remap.Add(new ButtonRemap { Pad = pad, Target = "B", Source = PadButton.East, LabelBased = true });
        }
    }

    public static void Reset(List<ButtonRemap> remap, EmulatedPad pad, string? target = null) =>
        remap.RemoveAll(r => r.Pad == pad && (target == null || r.Target == target));

    private static void Set(List<ButtonRemap> remap, EmulatedPad pad, ButtonRow row, PadButton? source)
    {
        remap.RemoveAll(r => r.Pad == pad && r.Target == row.Target);
        if (source != row.DefaultSource)
            remap.Add(new ButtonRemap { Pad = pad, Target = row.Target, Source = source });
    }

    /// <summary>Standardbelegung des Hubs: Gamepad-Taste → Konsolen-Taste.</summary>
    public static Dictionary<PadButton, string> Defaults(EmulatedPad pad, bool nintendoLayout) => pad switch
    {
        EmulatedPad.Wiimote => Wiimote(nintendoLayout),
        EmulatedPad.GameCube => GameCube(nintendoLayout),
        EmulatedPad.WiiUPro or EmulatedPad.WiiUGamePad or EmulatedPad.SwitchPro or EmulatedPad.SwitchJoyConPair => Modern(pad),
        EmulatedPad.NintendoDS => Ds(),
        EmulatedPad.Nintendo3DS => ThreeDs(),
        _ => [],
    };

    /// <summary>SDL-Gamecontroller-Name einer Gamepad-Taste (Eden, SDL-Mappings).</summary>
    public static string SdlName(PadButton b) => b switch
    {
        PadButton.South => "a",
        PadButton.East => "b",
        PadButton.West => "x",
        PadButton.North => "y",
        PadButton.L1 => "leftshoulder",
        PadButton.R1 => "rightshoulder",
        PadButton.L2 => "lefttrigger",
        PadButton.R2 => "righttrigger",
        PadButton.L3 => "leftstick",
        PadButton.R3 => "rightstick",
        PadButton.Back => "back",
        PadButton.Start => "start",
        PadButton.Guide => "guide",
        PadButton.DUp => "dpup",
        PadButton.DDown => "dpdown",
        PadButton.DLeft => "dpleft",
        PadButton.DRight => "dpright",
        _ => "",
    };

    /// <summary>Dolphin, Wii Remote + Nunchuk (DolphinInputWriter.WiimoteMapping).</summary>
    private static Dictionary<PadButton, string> Wiimote(bool nintendoLayout) => new()
    {
        [nintendoLayout ? PadButton.East : PadButton.South] = "A",
        [PadButton.R2] = "B",
        [nintendoLayout ? PadButton.South : PadButton.West] = "1",
        [nintendoLayout ? PadButton.North : PadButton.East] = "2",
        [PadButton.Back] = "−",
        [PadButton.Start] = "+",
        [PadButton.Guide] = "Home",
        [PadButton.DUp] = "Steuerkreuz ↑",
        [PadButton.DDown] = "Steuerkreuz ↓",
        [PadButton.DLeft] = "Steuerkreuz ←",
        [PadButton.DRight] = "Steuerkreuz →",
        [PadButton.RightStick] = "Zeiger (auf den Bildschirm zeigen)",
        [PadButton.R3] = "Wii Remote schütteln",
        [PadButton.L1] = "Nunchuk C",
        [PadButton.L2] = "Nunchuk Z",
        [PadButton.LeftStick] = "Nunchuk-Stick",
        [PadButton.L3] = "Nunchuk schütteln",
    };

    /// <summary>Dolphin, GameCube-Controller (DolphinInputWriter.GameCubeMapping).</summary>
    private static Dictionary<PadButton, string> GameCube(bool nintendoLayout)
    {
        var (a, b, x, y) = nintendoLayout
            ? (PadButton.East, PadButton.South, PadButton.North, PadButton.West)
            : (PadButton.South, PadButton.West, PadButton.East, PadButton.North);
        return new()
        {
            [a] = "A",
            [b] = "B",
            [x] = "X",
            [y] = "Y",
            [PadButton.R1] = "Z",
            [PadButton.L2] = "L",
            [PadButton.R2] = "R",
            [PadButton.Start] = "Start/Pause",
            [PadButton.LeftStick] = "Control-Stick",
            [PadButton.L3] = "Control-Stick langsam",
            [PadButton.RightStick] = "C-Stick",
            [PadButton.DUp] = "Steuerkreuz ↑",
            [PadButton.DDown] = "Steuerkreuz ↓",
            [PadButton.DLeft] = "Steuerkreuz ←",
            [PadButton.DRight] = "Steuerkreuz →",
        };
    }

    /// <summary>
    /// Cemu (Wii U Pro) und Eden (Switch): Belegung nach Position – die rechte Taste ist A, die untere B,
    /// die obere X, die linke Y (wie auf Nintendo-Controllern), unabhängig von der Beschriftung.
    /// </summary>
    private static Dictionary<PadButton, string> Modern(EmulatedPad pad)
    {
        var map = new Dictionary<PadButton, string>
        {
            [PadButton.East] = "A",
            [PadButton.South] = "B",
            [PadButton.North] = "X",
            [PadButton.West] = "Y",
            [PadButton.L1] = "L",
            [PadButton.R1] = "R",
            [PadButton.L2] = "ZL",
            [PadButton.R2] = "ZR",
            [PadButton.Start] = "+",
            [PadButton.Back] = "−",
            [PadButton.LeftStick] = "Linker Stick",
            [PadButton.RightStick] = "Rechter Stick",
            [PadButton.L3] = "Linken Stick drücken",
            [PadButton.R3] = "Rechten Stick drücken",
            [PadButton.DUp] = "Steuerkreuz ↑",
            [PadButton.DDown] = "Steuerkreuz ↓",
            [PadButton.DLeft] = "Steuerkreuz ←",
            [PadButton.DRight] = "Steuerkreuz →",
        };
        // Home: Eden belegt es (guide), Cemus SDL-Belegung ebenfalls
        map[PadButton.Guide] = "Home";
        return map;
    }

    /// <summary>
    /// melonDS (Nintendo DS): Belegung nach Position wie auf dem DS – rechts A, unten B, oben X, links Y.
    /// Der linke Stick steuert zusätzlich das Steuerkreuz; den Touchscreen bedient die Maus.
    /// </summary>
    private static Dictionary<PadButton, string> Ds() => new()
    {
        [PadButton.East] = "A",
        [PadButton.South] = "B",
        [PadButton.North] = "X",
        [PadButton.West] = "Y",
        [PadButton.L1] = "L",
        [PadButton.R1] = "R",
        [PadButton.Start] = "Start",
        [PadButton.Back] = "Select",
        [PadButton.DUp] = "Steuerkreuz ↑",
        [PadButton.DDown] = "Steuerkreuz ↓",
        [PadButton.DLeft] = "Steuerkreuz ←",
        [PadButton.DRight] = "Steuerkreuz →",
        [PadButton.LeftStick] = "Steuerkreuz (Stick)",
    };

    /// <summary>Azahar (Nintendo 3DS): nach Position wie auf dem 3DS, Circle Pad = linker, C-Stick = rechter Stick.</summary>
    private static Dictionary<PadButton, string> ThreeDs() => new()
    {
        [PadButton.East] = "A",
        [PadButton.South] = "B",
        [PadButton.North] = "X",
        [PadButton.West] = "Y",
        [PadButton.L1] = "L",
        [PadButton.R1] = "R",
        [PadButton.L2] = "ZL",
        [PadButton.R2] = "ZR",
        [PadButton.Start] = "Start",
        [PadButton.Back] = "Select",
        [PadButton.Guide] = "Home",
        [PadButton.DUp] = "Steuerkreuz ↑",
        [PadButton.DDown] = "Steuerkreuz ↓",
        [PadButton.DLeft] = "Steuerkreuz ←",
        [PadButton.DRight] = "Steuerkreuz →",
        [PadButton.LeftStick] = "Circle Pad",
        [PadButton.RightStick] = "C-Stick",
    };

    /// <summary>Name des emulierten Controllers.</summary>
    public static string PadTitle(EmulatedPad pad) => pad switch
    {
        EmulatedPad.GameCube => "GameCube-Controller",
        EmulatedPad.Wiimote => "Wii Remote + Nunchuk",
        EmulatedPad.WiiUPro => "Wii U Pro Controller",
        EmulatedPad.WiiUGamePad => "Wii U GamePad",
        EmulatedPad.SwitchPro => "Switch Pro Controller",
        EmulatedPad.SwitchJoyConPair => "Joy-Con-Paar",
        EmulatedPad.NintendoDS => "Nintendo DS",
        EmulatedPad.Nintendo3DS => "Nintendo 3DS",
        _ => "Controller",
    };

    /// <summary>Beschriftung einer Gamepad-Taste je nach Controller-Typ (Standard: PS5/DualSense).</summary>
    public static string Label(PadButton b, ControllerKind kind)
    {
        var face = kind switch
        {
            ControllerKind.Xbox or ControllerKind.Generic => (s: "A", e: "B", w: "X", n: "Y"),
            ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiUPro => (s: "B", e: "A", w: "Y", n: "X"),
            _ => (s: "✕ Kreuz", e: "○ Kreis", w: "□ Quadrat", n: "△ Dreieck"),
        };
        var ps = kind is not (ControllerKind.Xbox or ControllerKind.Generic or ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiUPro);
        var xbox = kind is ControllerKind.Xbox or ControllerKind.Generic;
        return b switch
        {
            PadButton.South => face.s,
            PadButton.East => face.e,
            PadButton.West => face.w,
            PadButton.North => face.n,
            PadButton.L1 => ps ? "L1" : xbox ? "LB" : "L",
            PadButton.R1 => ps ? "R1" : xbox ? "RB" : "R",
            PadButton.L2 => ps ? "L2" : xbox ? "LT" : "ZL",
            PadButton.R2 => ps ? "R2" : xbox ? "RT" : "ZR",
            PadButton.L3 => ps ? "L3 (linken Stick drücken)" : "Linken Stick drücken",
            PadButton.R3 => ps ? "R3 (rechten Stick drücken)" : "Rechten Stick drücken",
            PadButton.Back => ps ? "Create" : xbox ? "View" : "−",
            PadButton.Start => ps ? "Options" : xbox ? "Menu" : "+",
            PadButton.Guide => ps ? "PS-Taste" : xbox ? "Xbox-Taste" : "Home",
            PadButton.DUp => "Steuerkreuz ↑",
            PadButton.DDown => "Steuerkreuz ↓",
            PadButton.DLeft => "Steuerkreuz ←",
            PadButton.DRight => "Steuerkreuz →",
            PadButton.LeftStick => "Linker Stick",
            PadButton.RightStick => "Rechter Stick",
            _ => b.ToString(),
        };
    }
}
