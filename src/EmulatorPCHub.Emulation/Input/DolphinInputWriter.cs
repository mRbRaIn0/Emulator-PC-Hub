using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Dolphin: schreibt GCPadNew.ini, WiimoteNew.ini und die Port-Belegung (Dolphin.ini) für Spieler 1–4.
/// Xbox → XInput-Backend, PS/Nintendo → SDL-Backend, Tastatur → DInput, echte Wii Remote → Durchreichung.
/// Eingabenamen entsprechen Dolphins SDL-Backend (Button S/E/W/N, Pad N/S/W/E, Left Y+ = oben).
/// </summary>
public sealed class DolphinInputWriter : IInputConfigWriter
{
    private readonly string _userDir;
    private readonly BackupService? _backups;

    public DolphinInputWriter(string dolphinUserDir, BackupService? backups)
    {
        _userDir = dolphinUserDir;
        _backups = backups;
    }

    private string ConfigDir => Path.Combine(_userDir, "Config");

    public string Apply(InputSetup setup, GameEntry game)
    {
        Directory.CreateDirectory(ConfigDir);
        var gcPath = Path.Combine(ConfigDir, "GCPadNew.ini");
        var wmPath = Path.Combine(ConfigDir, "WiimoteNew.ini");
        var dolphinIni = new IniFile(Path.Combine(ConfigDir, "Dolphin.ini"));
        BackupOnce(gcPath);
        BackupOnce(wmPath);

        var gc = new List<string>();
        var wm = new List<string>();
        var anyRealWiimote = false;
        for (int p = 1; p <= 4; p++)
        {
            var player = setup.Players.FirstOrDefault(x => x.Player == p);
            gc.Add($"[GCPad{p}]");
            wm.Add($"[Wiimote{p}]");
            if (player == null)
            {
                dolphinIni.Set("Core", $"SIDevice{p - 1}", "0");
                if (setup.RealWiimotesInFreeSlots)
                {
                    // Freier Platz: eine gekoppelte echte Wii Remote darf sich hier verbinden
                    wm.Add("Source = 2");
                    anyRealWiimote = true;
                }
                else
                {
                    wm.Add("Source = 0");
                }
                wm.Add("");
                continue;
            }
            var device = player.Device;
            switch (player.Pad)
            {
                case EmulatedPad.GameCube:
                    dolphinIni.Set("Core", $"SIDevice{p - 1}", "6");
                    gc.AddRange(WithRemap(GameCubeMapping(device, setup.Rumble), device, EmulatedPad.GameCube, setup.Remap, GameCubeKeys));
                    wm.Add("Source = 0");
                    break;
                default:
                    dolphinIni.Set("Core", $"SIDevice{p - 1}", "0");
                    if (device.Kind == ControllerKind.WiiRemote && player.Mode == PlayerMode.Native)
                    {
                        wm.Add("Source = 2");
                        anyRealWiimote = true;
                    }
                    else
                    {
                        wm.Add("Source = 1");
                        wm.AddRange(WithRemap(WiimoteMapping(device, setup.Rumble), device, EmulatedPad.Wiimote, setup.Remap, WiimoteKeys));
                    }
                    break;
            }
            gc.Add("");
            wm.Add("");
        }
        if (anyRealWiimote)
            dolphinIni.Set("Core", "WiimoteContinuousScanning", "True");
        dolphinIni.Save();
        File.WriteAllLines(gcPath, gc);
        File.WriteAllLines(wmPath, wm);
        return $"Dolphin: {setup.Players.Count} Spieler konfiguriert";
    }

    // Konsolen-Taste (Zielname aus ButtonMap) → Schlüssel in GCPadNew.ini / WiimoteNew.ini
    private static readonly Dictionary<string, string[]> GameCubeKeys = new()
    {
        ["A"] = ["Buttons/A"], ["B"] = ["Buttons/B"], ["X"] = ["Buttons/X"], ["Y"] = ["Buttons/Y"], ["Z"] = ["Buttons/Z"],
        ["Start/Pause"] = ["Buttons/Start"], ["L"] = ["Triggers/L", "Triggers/L-Analog"], ["R"] = ["Triggers/R", "Triggers/R-Analog"],
        ["Control-Stick langsam"] = ["Main Stick/Modifier"],
        ["Steuerkreuz ↑"] = ["D-Pad/Up"], ["Steuerkreuz ↓"] = ["D-Pad/Down"], ["Steuerkreuz ←"] = ["D-Pad/Left"], ["Steuerkreuz →"] = ["D-Pad/Right"],
    };

    private static readonly Dictionary<string, string[]> WiimoteKeys = new()
    {
        ["A"] = ["Buttons/A"], ["B"] = ["Buttons/B"], ["1"] = ["Buttons/1"], ["2"] = ["Buttons/2"],
        ["−"] = ["Buttons/-"], ["+"] = ["Buttons/+"], ["Home"] = ["Buttons/Home"],
        ["Steuerkreuz ↑"] = ["D-Pad/Up"], ["Steuerkreuz ↓"] = ["D-Pad/Down"], ["Steuerkreuz ←"] = ["D-Pad/Left"], ["Steuerkreuz →"] = ["D-Pad/Right"],
        ["Wii Remote schütteln"] = ["Shake/X", "Shake/Y", "Shake/Z"],
        ["Nunchuk C"] = ["Nunchuk/Buttons/C"], ["Nunchuk Z"] = ["Nunchuk/Buttons/Z"],
        ["Nunchuk schütteln"] = ["Nunchuk/Shake/X", "Nunchuk/Shake/Y", "Nunchuk/Shake/Z"],
    };

    /// <summary>Eigene Umbelegung (ButtonMap) über die Standardbelegung legen; gilt nicht für die Tastatur.</summary>
    private static IEnumerable<string> WithRemap(IEnumerable<string> lines, ControllerDescriptor d, EmulatedPad pad,
        IReadOnlyList<ButtonRemap> remap, Dictionary<string, string[]> keys)
    {
        var list = lines.ToList();
        if (d.IsKeyboard)
            return list;
        var n = NamesFor(d);
        var rows = ButtonMap.For(pad, d.NintendoLayout, remap);
        foreach (var row in rows.Where(r => r.Changed))
        {
            if (!keys.TryGetValue(row.Target, out var iniKeys))
                continue;
            var value = row.Source is { } src ? InputName(n, src) : "";
            // A (Bestätigen): Bleibt die bisherige Taste dadurch unbelegt, löst sie weiterhin A aus (Dolphin-Ausdruck
            // „neu | alt“) – z. B. Gas auf R2 und trotzdem ✕ zum Bestätigen im Menü.
            if (row.Target == "A" && value.Length > 0 && rows.All(r => r.Source != row.DefaultSource))
                value = $"{value} | {InputName(n, row.DefaultSource)}";
            foreach (var key in iniKeys)
            {
                var i = list.FindIndex(l => l.StartsWith(key + " = ", StringComparison.Ordinal));
                if (i >= 0)
                    list[i] = $"{key} = {value}";
                else
                    list.Add($"{key} = {value}");
            }
        }
        return list;
    }

    private static string InputName(Names n, PadButton b) => b switch
    {
        PadButton.South => n.South,
        PadButton.East => n.East,
        PadButton.West => n.West,
        PadButton.North => n.North,
        PadButton.L1 => n.ShoulderL,
        PadButton.R1 => n.ShoulderR,
        PadButton.L2 => n.TriggerL,
        PadButton.R2 => n.TriggerR,
        PadButton.L3 => n.ThumbL,
        PadButton.R3 => n.ThumbR,
        PadButton.Back => n.Back,
        PadButton.Start => n.Start,
        PadButton.Guide => n.Guide,
        PadButton.DUp => n.DUp,
        PadButton.DDown => n.DDown,
        PadButton.DLeft => n.DLeft,
        PadButton.DRight => n.DRight,
        _ => "",
    };

    private void BackupOnce(string file)
    {
        // Das Original des Nutzers einmalig sichern, danach verwaltet der Hub die Datei.
        var marker = file + ".hub-original";
        if (File.Exists(file) && !File.Exists(marker))
        {
            File.Copy(file, marker);
            _backups?.BackupFile(BackupCategory.Config, file, "dolphin-" + Path.GetFileNameWithoutExtension(file));
        }
    }

    /// <summary>Gerätestring und Namen der Eingaben je Gerätetyp.</summary>
    private sealed record Names(string Device, string South, string East, string West, string North, string Start, string Back,
        string Guide, string ShoulderL, string ShoulderR, string TriggerL, string TriggerR, string ThumbL, string ThumbR,
        string DUp, string DDown, string DLeft, string DRight,
        string LUp, string LDown, string LLeft, string LRight, string RUp, string RDown, string RLeft, string RRight,
        string? MotorL, string? MotorR);

    private static Names NamesFor(ControllerDescriptor d)
    {
        if (d.IsKeyboard)
        {
            // Einheitliches Tastatur-Layout des Hubs (WASD/IJKL, Pfeiltasten)
            // A=Z, B=X, X=C, Y=V (South/East/North/West werden unten für die Tastatur so ausgewertet)
            return new Names("DInput/0/Keyboard Mouse", "Z", "X", "V", "C", "RETURN", "MINUS", "HOME", "E", "U", "Q", "O", "F", "H",
                "UP", "DOWN", "LEFT", "RIGHT", "W", "S", "A", "D", "I", "K", "J", "L", null, null);
        }
        if (d.Kind == ControllerKind.Xbox && d.XInputIndex is { } x)
        {
            return new Names($"XInput/{x}/Gamepad", "`Button A`", "`Button B`", "`Button X`", "`Button Y`", "Start", "Back", "Guide",
                "`Shoulder L`", "`Shoulder R`", "`Trigger L`", "`Trigger R`", "`Thumb L`", "`Thumb R`",
                "`Pad N`", "`Pad S`", "`Pad W`", "`Pad E`", "`Left Y+`", "`Left Y-`", "`Left X-`", "`Left X+`",
                "`Right Y+`", "`Right Y-`", "`Right X-`", "`Right X+`", "`Motor L`", "`Motor R`");
        }
        return new Names($"SDL/{d.NameIndex}/{d.Name}", "`Button S`", "`Button E`", "`Button W`", "`Button N`", "Start", "Back", "Guide",
            "`Shoulder L`", "`Shoulder R`", "`Trigger L`", "`Trigger R`", "`Thumb L`", "`Thumb R`",
            "`Pad N`", "`Pad S`", "`Pad W`", "`Pad E`", "`Left Y+`", "`Left Y-`", "`Left X-`", "`Left X+`",
            "`Right Y+`", "`Right Y-`", "`Right X-`", "`Right X+`", "`Motor L`", "`Motor R`");
    }

    private static IEnumerable<string> GameCubeMapping(ControllerDescriptor d, bool rumble)
    {
        var n = NamesFor(d);
        // GC-A = Knopf mit Beschriftung A (Nintendo-Layout: rechts), GC-B = Knopf links daneben
        var (a, b, x, y) = d.NintendoLayout ? (n.East, n.South, n.North, n.West) : (n.South, n.West, n.East, n.North);
        if (d.IsKeyboard)
            (a, b, x, y) = (n.South, n.East, n.North, n.West);
        yield return $"Device = {n.Device}";
        yield return $"Buttons/A = {a}";
        yield return $"Buttons/B = {b}";
        yield return $"Buttons/X = {x}";
        yield return $"Buttons/Y = {y}";
        yield return $"Buttons/Z = {n.ShoulderR}";
        yield return $"Buttons/Start = {n.Start}";
        yield return $"Main Stick/Up = {n.LUp}";
        yield return $"Main Stick/Down = {n.LDown}";
        yield return $"Main Stick/Left = {n.LLeft}";
        yield return $"Main Stick/Right = {n.LRight}";
        yield return $"Main Stick/Modifier = {n.ThumbL}";
        yield return $"C-Stick/Up = {n.RUp}";
        yield return $"C-Stick/Down = {n.RDown}";
        yield return $"C-Stick/Left = {n.RLeft}";
        yield return $"C-Stick/Right = {n.RRight}";
        yield return $"Triggers/L = {n.TriggerL}";
        yield return $"Triggers/R = {n.TriggerR}";
        yield return $"Triggers/L-Analog = {n.TriggerL}";
        yield return $"Triggers/R-Analog = {n.TriggerR}";
        yield return $"D-Pad/Up = {n.DUp}";
        yield return $"D-Pad/Down = {n.DDown}";
        yield return $"D-Pad/Left = {n.DLeft}";
        yield return $"D-Pad/Right = {n.DRight}";
        if (rumble && n.MotorL != null)
            yield return $"Rumble/Motor = {n.MotorL}|{n.MotorR}";
    }

    private static IEnumerable<string> WiimoteMapping(ControllerDescriptor d, bool rumble)
    {
        var n = NamesFor(d);
        var (a, b) = d.NintendoLayout ? (n.East, n.South) : (n.South, n.West);
        yield return $"Device = {n.Device}";
        yield return $"Buttons/A = {a}";
        yield return $"Buttons/B = {n.TriggerR}";
        yield return $"Buttons/1 = {b}";
        yield return $"Buttons/2 = {(d.NintendoLayout ? n.North : n.East)}";
        yield return $"Buttons/- = {n.Back}";
        yield return $"Buttons/+ = {n.Start}";
        yield return $"Buttons/Home = {n.Guide}";
        yield return $"D-Pad/Up = {n.DUp}";
        yield return $"D-Pad/Down = {n.DDown}";
        yield return $"D-Pad/Left = {n.DLeft}";
        yield return $"D-Pad/Right = {n.DRight}";
        yield return $"IR/Up = {n.RUp}";
        yield return $"IR/Down = {n.RDown}";
        yield return $"IR/Left = {n.RLeft}";
        yield return $"IR/Right = {n.RRight}";
        yield return $"Shake/X = {n.ThumbR}";
        yield return $"Shake/Y = {n.ThumbR}";
        yield return $"Shake/Z = {n.ThumbR}";
        yield return "Extension = Nunchuk";
        yield return $"Nunchuk/Buttons/C = {n.ShoulderL}";
        yield return $"Nunchuk/Buttons/Z = {n.TriggerL}";
        yield return $"Nunchuk/Stick/Up = {n.LUp}";
        yield return $"Nunchuk/Stick/Down = {n.LDown}";
        yield return $"Nunchuk/Stick/Left = {n.LLeft}";
        yield return $"Nunchuk/Stick/Right = {n.LRight}";
        yield return $"Nunchuk/Shake/X = {n.ThumbL}";
        yield return $"Nunchuk/Shake/Y = {n.ThumbL}";
        yield return $"Nunchuk/Shake/Z = {n.ThumbL}";
        if (rumble && n.MotorL != null)
            yield return $"Rumble/Motor = {n.MotorL}|{n.MotorR}";
    }
}
