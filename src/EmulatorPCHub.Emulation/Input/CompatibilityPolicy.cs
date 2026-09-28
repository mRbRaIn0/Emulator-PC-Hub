using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Entscheidet pro Spieler, ob ein Controller direkt (nativ) an das Spiel geht oder über die
/// Kompatibilitätsschicht (PadForge, virtueller Xbox-Controller). Xbox und Tastatur laufen immer nativ;
/// Sony- und Nintendo-Controller nur dann über PadForge, wenn das jeweilige Backend sie nicht direkt unterstützt.
/// </summary>
public static class CompatibilityPolicy
{
    public static (PlayerMode Mode, string Reason) Decide(string backendId, ControllerKind kind, PlayerMode requested)
    {
        if (requested == PlayerMode.Native)
            return (PlayerMode.Native, "manuell: nativ");
        if (requested == PlayerMode.Compatibility)
            return (PlayerMode.Compatibility, "manuell: PadForge");

        if (kind is ControllerKind.Xbox)
            return (PlayerMode.Native, "Xbox-Controller laufen nativ (XInput)");
        if (kind is ControllerKind.Keyboard)
            return (PlayerMode.Native, "Tastatur wird direkt unterstützt");

        var native = Supports(backendId, kind);
        return native
            ? (PlayerMode.Native, $"{Backend(backendId)} unterstützt {kind.DisplayName()} direkt")
            : (PlayerMode.Compatibility, $"{Backend(backendId)} unterstützt {kind.DisplayName()} nicht direkt → PadForge (virtueller Xbox-Controller)");
    }

    /// <summary>Unterstützt das Backend den Controller-Typ ohne Zusatzsoftware?</summary>
    public static bool Supports(string backendId, ControllerKind kind) => backendId switch
    {
        // Dolphin: SDL + XInput + echte Wii Remotes (Bluetooth-Durchreichung)
        EmulatorIds.Dolphin => kind is not ControllerKind.Generic,
        // Cemu: SDL-Controller (DualSense, DualShock, Switch Pro, Joy-Con, Wii U Pro); Wii Remote nur im Spezialmodus
        EmulatorIds.Cemu => kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4 or ControllerKind.SwitchPro
            or ControllerKind.JoyCon or ControllerKind.WiiUPro,
        // Eden (SDL2): Gamepads inkl. DualSense, Switch Pro, Joy-Con; keine Wii Remote
        EmulatorIds.Switch => kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4 or ControllerKind.SwitchPro
            or ControllerKind.JoyCon or ControllerKind.WiiUPro,
        // WiiCompiled: native PC-Version mit SDL-Gamepad-Eingabe
        EmulatorIds.WiiCompiled => kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4 or ControllerKind.SwitchPro
            or ControllerKind.JoyCon or ControllerKind.WiiUPro,
        // melonDS / Azahar: SDL-Gamepads und Tastatur, Belegung im Emulator selbst
        EmulatorIds.MelonDS or EmulatorIds.Azahar => kind is ControllerKind.PlayStation5 or ControllerKind.PlayStation4
            or ControllerKind.SwitchPro or ControllerKind.JoyCon or ControllerKind.WiiUPro,
        _ => false,
    };

    /// <summary>
    /// Switch-Spiele, die keinen Pro Controller akzeptieren (Bewegungsspiele) – dort wird ein Joy-Con-Paar emuliert,
    /// die Bewegungssensoren des Gamepads (z. B. DualSense) steuern dann die Joy-Cons.
    /// </summary>
    public static readonly IReadOnlySet<string> JoyConOnlyTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "0100D2F00D5C0000", // Nintendo Switch Sports
        "0100BEE017FC0000", // Just Dance 2025 Edition
    };

    /// <summary>Welcher Controller im Spiel emuliert wird, wenn das Profil „Auto“ sagt.</summary>
    public static EmulatedPad DefaultPad(string backendId, GameEntry game, ControllerDescriptor device) => backendId switch
    {
        EmulatorIds.Dolphin when game.Platform == HubPlatform.GameCube => EmulatedPad.GameCube,
        // Mario Kart Wii ist mit GameCube-Belegung auf Gamepads am angenehmsten; echte Wii Remotes bleiben Wii Remotes.
        EmulatorIds.Dolphin when game.Special == SpecialPage.MarioKartWii =>
            device.Kind == ControllerKind.WiiRemote ? EmulatedPad.Wiimote : EmulatedPad.GameCube,
        EmulatorIds.Dolphin => EmulatedPad.Wiimote,
        EmulatorIds.Cemu => EmulatedPad.WiiUPro,
        EmulatorIds.Switch => device.Kind == ControllerKind.JoyCon || JoyConOnlyTitles.Contains(game.GameCode ?? "")
            ? EmulatedPad.SwitchJoyConPair
            : EmulatedPad.SwitchPro,
        EmulatorIds.MelonDS => EmulatedPad.NintendoDS,
        EmulatorIds.Azahar => EmulatedPad.Nintendo3DS,
        _ => EmulatedPad.Auto,
    };

    private static string Backend(string id) => id switch
    {
        EmulatorIds.Dolphin => "Dolphin",
        EmulatorIds.Cemu => "Cemu",
        EmulatorIds.Switch => "Der Switch-Emulator",
        EmulatorIds.WiiCompiled => "WiiCompiled",
        EmulatorIds.MelonDS => "melonDS",
        EmulatorIds.Azahar => "Azahar",
        _ => id,
    };
}

/// <summary>Schreibt die Controller-Konfiguration eines Backends für einen Spielstart.</summary>
public interface IInputConfigWriter
{
    /// <summary>Kurzbeschreibung, was geschrieben wird (für Log/Overlay).</summary>
    string Apply(InputSetup setup, GameEntry game);
}
