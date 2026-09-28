using System.Text.Json.Serialization;

namespace EmulatorPCHub.Core.Input;

[JsonConverter(typeof(JsonStringEnumConverter<ControllerKind>))]
public enum ControllerKind
{
    Generic,
    Xbox,
    PlayStation5,
    PlayStation4,
    SwitchPro,
    JoyCon,
    WiiRemote,
    WiiUPro,
    Keyboard,
}

public static class ControllerKindInfo
{
    public static string DisplayName(this ControllerKind k) => k switch
    {
        ControllerKind.Xbox => "Xbox",
        ControllerKind.PlayStation5 => "PS5 / DualSense",
        ControllerKind.PlayStation4 => "PS4 / DualShock 4",
        ControllerKind.SwitchPro => "Switch Pro",
        ControllerKind.JoyCon => "Joy-Con",
        ControllerKind.WiiRemote => "Wii Remote",
        ControllerKind.WiiUPro => "Wii U Pro",
        ControllerKind.Keyboard => "Tastatur",
        _ => "Gamepad",
    };

    /// <summary>Segoe-Fluent-Symbol für die Anzeige.</summary>
    public static string Glyph(this ControllerKind k) => k == ControllerKind.Keyboard ? "" : "";

    public static bool IsNintendo(this ControllerKind k) =>
        k is ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiRemote or ControllerKind.WiiUPro;
}

/// <summary>
/// Ein konkretes Eingabegerät, wie es an Emulatoren übergeben wird.
/// Enthält die IDs, die Dolphin, Cemu und Eden für dasselbe Gerät erwarten.
/// </summary>
public sealed record ControllerDescriptor
{
    /// <summary>Stabiler Schlüssel (GUID + Seriennummer bzw. „keyboard“) für die Spieler-Zuordnung.</summary>
    public required string Key { get; init; }
    public required string Name { get; init; }
    public ControllerKind Kind { get; init; }
    /// <summary>SDL-GUID als Hex-String (32 Zeichen).</summary>
    public string? SdlGuid { get; init; }
    /// <summary>SDL-Mapping-String („a:b0,b:b1,leftx:a0,dpup:h0.1,…“) – Rohbelegung für Eden.</summary>
    public string? SdlMapping { get; init; }
    /// <summary>Position in der SDL-Geräteliste (Reihenfolge einer frischen Erkennung) – melonDS „JoystickID“.</summary>
    public int SdlIndex { get; init; }
    /// <summary>Wie viele Geräte mit gleicher GUID vor diesem liegen (für Cemu/Eden-IDs).</summary>
    public int GuidIndex { get; init; }
    /// <summary>Wie viele Geräte mit gleichem Namen vor diesem liegen (für Dolphin „SDL/n/Name“).</summary>
    public int NameIndex { get; init; }
    /// <summary>XInput-Slot (0–3) bei Xbox-/XInput-Geräten.</summary>
    public int? XInputIndex { get; init; }
    /// <summary>Knopf mit Beschriftung „A“ liegt rechts (Nintendo-Layout).</summary>
    public bool NintendoLayout { get; init; }
    /// <summary>Virtueller Controller (z. B. von PadForge erzeugt).</summary>
    public bool IsVirtual { get; init; }

    public bool IsKeyboard => Kind == ControllerKind.Keyboard;

    public static ControllerDescriptor Keyboard { get; } = new() { Key = "keyboard", Name = "Tastatur", Kind = ControllerKind.Keyboard };
}

/// <summary>Wie ein Spieler an das Spiel übergeben wird.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlayerMode>))]
public enum PlayerMode
{
    /// <summary>Hub entscheidet: nativ, wenn der Emulator das Gerät unterstützt, sonst Kompatibilitätsschicht.</summary>
    Auto,
    /// <summary>Gerät direkt an den Emulator.</summary>
    Native,
    /// <summary>Über PadForge als virtueller Xbox-Controller.</summary>
    Compatibility,
}

/// <summary>Tasten eines modernen Gamepads nach Position (PS5, Xbox, Switch Pro …).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PadButton>))]
public enum PadButton
{
    South, East, West, North,
    L1, R1, L2, R2, L3, R3,
    Back, Start, Guide,
    DUp, DDown, DLeft, DRight,
    LeftStick, RightStick,
}

/// <summary>Eigene Belegung einer Konsolen-Taste: <see cref="Target"/> liegt auf <see cref="Source"/> (null = nicht belegt).</summary>
public sealed class ButtonRemap
{
    [JsonPropertyName("pad")] public EmulatedPad Pad { get; set; }
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("source")] public PadButton? Source { get; set; }
    /// <summary>
    /// Gilt nur für Controller ohne Nintendo-Layout (PlayStation, Xbox …) – z. B. „Bestätigen mit ✕“.
    /// Auf Nintendo-Controllern bleibt die Standardbelegung (A = rechte Taste).
    /// </summary>
    [JsonPropertyName("labelBased")] public bool LabelBased { get; set; }
}

/// <summary>Welcher Controller im Spiel emuliert wird.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmulatedPad>))]
public enum EmulatedPad
{
    Auto,
    GameCube,
    Wiimote,
    WiiUPro,
    WiiUGamePad,
    SwitchPro,
    SwitchJoyConPair,
    NintendoDS,
    Nintendo3DS,
}

public sealed class PlayerBinding
{
    [JsonPropertyName("player")] public int Player { get; set; }
    /// <summary>Festes Gerät (Key) oder null = wer gerade auf diesem Spieler-Platz ist.</summary>
    [JsonPropertyName("device")] public string? DeviceKey { get; set; }
    [JsonPropertyName("mode")] public PlayerMode Mode { get; set; } = PlayerMode.Auto;
    [JsonPropertyName("pad")] public EmulatedPad Pad { get; set; } = EmulatedPad.Auto;
}

/// <summary>Controller-Profil pro Spiel (oder „default“).</summary>
public sealed class ControllerProfile
{
    [JsonPropertyName("game")] public string GameKey { get; set; } = "default";
    [JsonPropertyName("players")] public List<PlayerBinding> Players { get; set; } =
        [new() { Player = 1 }, new() { Player = 2 }, new() { Player = 3 }, new() { Player = 4 }];
    /// <summary>Spieler-Nummer der Tastatur (0 = aus; -1 = automatisch, wenn kein Controller auf P1).</summary>
    [JsonPropertyName("keyboardPlayer")] public int KeyboardPlayer { get; set; } = -1;
    /// <summary>Tastenbelegung in den Emulatoren durch den Hub verwalten.</summary>
    [JsonPropertyName("manageMappings")] public bool ManageMappings { get; set; } = true;
    [JsonPropertyName("rumble")] public bool Rumble { get; set; } = true;
    /// <summary>PadForge-Profil, das für dieses Spiel aktiviert wird (leer = Standard).</summary>
    [JsonPropertyName("padforgeProfile")] public string PadForgeProfile { get; set; } = "";
    /// <summary>Eigene Tastenbelegung (Abweichungen von der Hub-Standardbelegung) je emuliertem Controller.</summary>
    [JsonPropertyName("remap")] public List<ButtonRemap> Remap { get; set; } = [];

    public PlayerBinding For(int player)
    {
        var b = Players.FirstOrDefault(p => p.Player == player);
        if (b == null)
        {
            b = new PlayerBinding { Player = player };
            Players.Add(b);
        }
        return b;
    }
}

/// <summary>Aufgelöste Zuordnung für einen Spielstart.</summary>
public sealed record ResolvedPlayer(int Player, ControllerDescriptor Device, PlayerMode Mode, EmulatedPad Pad, string Reason);

public sealed class InputSetup
{
    public List<ResolvedPlayer> Players { get; } = [];
    public bool ManageMappings { get; init; } = true;
    public bool Rumble { get; init; } = true;
    /// <summary>Dolphin: Spielerplätze ohne Gerät mit echten Wii Remotes belegen (gekoppelt per Bluetooth/DolphinBar).</summary>
    public bool RealWiimotesInFreeSlots { get; init; }
    /// <summary>Eigene Tastenbelegung aus dem Profil (gilt nicht für die Tastatur).</summary>
    public IReadOnlyList<ButtonRemap> Remap { get; init; } = [];
    public bool NeedsCompatibilityLayer => Players.Any(p => p.Mode == PlayerMode.Compatibility);
}
