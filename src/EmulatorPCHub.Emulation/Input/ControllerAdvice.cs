using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>Wie gut ein Eingabegerät zu einem Spiel passt.</summary>
public enum InputFit
{
    /// <summary>Beste Wahl für dieses Spiel.</summary>
    Recommended,
    /// <summary>Geht ohne Einschränkung.</summary>
    Works,
    /// <summary>Geht, aber mit Abstrichen (z. B. Zeiger per Stick) oder nur mit Handgriff.</summary>
    Limited,
    /// <summary>Geht nicht bzw. ist nicht sinnvoll spielbar.</summary>
    No,
}

public sealed record InputAdvice(ControllerKind Kind, string Label, InputFit Fit, string Reason);

public sealed record ControllerAdviceResult(IReadOnlyList<InputAdvice> Items, string Style)
{
    public InputAdvice? Best => Items.Where(i => i.Fit == InputFit.Recommended).FirstOrDefault()
                                ?? Items.FirstOrDefault(i => i.Fit == InputFit.Works);
}

/// <summary>
/// Controller-Empfehlung pro Spiel für Tastatur, PS5-Controller und echte Wii Remote – ohne jedes Spiel zu testen:
/// Grundregel aus Plattform + Emulator (was der Hub dort automatisch einrichten kann), verfeinert über die Spielart
/// (Bewegung, MotionPlus, Zeiger, nur Wii Remote, Touchscreen …) für bekannte Spiele (Disc-ID bzw. Titel).
/// </summary>
public static class ControllerAdvisor
{
    /// <summary>Wie ein Spiel bedient wird.</summary>
    public enum PlayStyle
    {
        /// <summary>Normale Tasten/Sticks (GameCube, Pro Controller, Classic Controller).</summary>
        Buttons,
        /// <summary>Wii: Zeiger auf den Bildschirm (Menüs/Spiel), sonst Tasten.</summary>
        Pointer,
        /// <summary>Wii: Wii Remote + Nunchuk, gelegentlich Schütteln.</summary>
        Nunchuk,
        /// <summary>Wii: Schwingen/Bewegen ist das Spiel (Sport, Party).</summary>
        Motion,
        /// <summary>Wii: braucht Wii MotionPlus (1:1-Bewegung).</summary>
        MotionPlus,
        /// <summary>Wii U: Spieler nur mit Wii Remotes (kein Pro Controller).</summary>
        WiiRemoteOnly,
        /// <summary>Wii U: Touchscreen des GamePads ist zentral.</summary>
        GamePadTouch,
        /// <summary>Wii U: je nach Disziplin GamePad/Wii Remote nötig.</summary>
        Mixed,
        /// <summary>DS/3DS: ein Spieler, Touchscreen per Maus – Tastatur + Maus liegen am nächsten.</summary>
        Handheld,
        /// <summary>DS/3DS: schnelles Tastenspiel (Rennen, Sport) – Controller besser.</summary>
        HandheldAction,
        /// <summary>DS/3DS: fast nur Touchscreen (Stylus).</summary>
        HandheldTouch,
    }

    // Wii/GameCube: die ersten drei Zeichen der Disc-ID (regionsunabhängig)
    private static readonly Dictionary<string, PlayStyle> DiscStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RZT"] = PlayStyle.MotionPlus,   // Wii Sports Resort
        ["RSP"] = PlayStyle.Motion,       // Wii Sports
        ["SUP"] = PlayStyle.Motion,       // Wii Party
        ["RM8"] = PlayStyle.Motion,       // Mario Party 8
        ["RPX"] = PlayStyle.Motion,       // EA Playground
        ["RCG"] = PlayStyle.Motion,       // Carnival Games
        ["RYE"] = PlayStyle.Motion,       // 101-in-1 Party Megamix
        ["SOI"] = PlayStyle.Motion,       // 101-in-1 Sports
        ["RDX"] = PlayStyle.Motion,       // Sports Island
        ["RI6"] = PlayStyle.Motion,       // Summer Sports Party
        ["R4Q"] = PlayStyle.Nunchuk,      // Mario Strikers Charged
        ["RMK"] = PlayStyle.Nunchuk,      // Mario Sports Mix
        ["SKY"] = PlayStyle.Nunchuk,      // Skylanders Giants
        ["SSP"] = PlayStyle.Nunchuk,      // Skylanders Spyro's Adventure
        ["R9F"] = PlayStyle.Buttons,      // F1 2009 (auch GameCube-/Classic-Controller)
        ["RMC"] = PlayStyle.Buttons,      // Mario Kart Wii (GameCube-Belegung)
    };

    // Titel-Schlüsselwörter je Plattform (Wii U, eShop und Spiele ohne bekannte ID) – erster Treffer gilt
    private static readonly (string Key, HubPlatform Platform, PlayStyle Style)[] TitleStyles =
    [
        ("wii sports club", HubPlatform.WiiU, PlayStyle.WiiRemoteOnly),
        ("wii party u", HubPlatform.WiiU, PlayStyle.WiiRemoteOnly),
        ("mario party 10", HubPlatform.WiiU, PlayStyle.WiiRemoteOnly),
        ("just dance", HubPlatform.WiiU, PlayStyle.WiiRemoteOnly),
        ("super mario maker", HubPlatform.WiiU, PlayStyle.GamePadTouch),
        ("tabletop gallery", HubPlatform.WiiU, PlayStyle.GamePadTouch),
        ("olympic", HubPlatform.WiiU, PlayStyle.Mixed),
        ("wii sports resort", HubPlatform.Wii, PlayStyle.MotionPlus),
        ("wii sports", HubPlatform.Wii, PlayStyle.Motion),
        ("wii party", HubPlatform.Wii, PlayStyle.Motion),
        ("mario party 8", HubPlatform.Wii, PlayStyle.Motion),
        ("just dance", HubPlatform.Wii, PlayStyle.Motion),
        ("skylanders", HubPlatform.Wii, PlayStyle.Nunchuk),
    ];

    // DS/3DS: Titel, die sich mit Controller besser spielen bzw. fast nur per Touchscreen laufen
    private static readonly string[] HandheldActionKeys = ["mario kart", "mario sports superstars", "new super mario", "super mario 3d"];
    private static readonly string[] HandheldTouchKeys = ["mario vs", "tomodachi", "brain", "nintendogs", "kirby canvas"];

    /// <summary>Spielart bestimmen: Disc-ID vor Titel, sonst Grundregel der Plattform.</summary>
    public static PlayStyle StyleFor(GameEntry game)
    {
        if (game.Platform is HubPlatform.DS or HubPlatform.ThreeDS)
        {
            var t = game.Title.ToLowerInvariant();
            if (HandheldActionKeys.Any(t.Contains))
                return PlayStyle.HandheldAction;
            if (HandheldTouchKeys.Any(t.Contains))
                return PlayStyle.HandheldTouch;
            return PlayStyle.Handheld;
        }
        if (game.Special is SpecialPage.MarioKartWii or SpecialPage.MarioKart8Deluxe)
            return PlayStyle.Buttons;
        if (game.Platform is HubPlatform.Wii or HubPlatform.GameCube && game.GameCode is { Length: >= 3 } code
            && DiscStyles.TryGetValue(code[..3], out var disc))
            return disc;
        var title = game.Title.ToLowerInvariant();
        foreach (var (key, platform, style) in TitleStyles)
        {
            if (platform == game.Platform && title.Contains(key))
                return style;
        }
        return game.Platform == HubPlatform.Wii ? PlayStyle.Pointer : PlayStyle.Buttons;
    }

    /// <summary>Empfehlung für Tastatur, PS5 und Wii Remote; <paramref name="backendId"/> = Emulator, mit dem das Spiel startet.</summary>
    public static ControllerAdviceResult For(GameEntry game, string backendId)
    {
        var style = StyleFor(game);
        return new ControllerAdviceResult([Keyboard(game, backendId, style), Pad(game, style), Wiimote(game, backendId, style)], StyleName(style));
    }

    private static InputAdvice Keyboard(GameEntry _, string backend, PlayStyle style)
    {
        (InputFit fit, string why) = style switch
        {
            PlayStyle.MotionPlus => (InputFit.No, "1:1-Bewegung (MotionPlus) lässt sich mit Tasten nicht nachbilden"),
            PlayStyle.Motion => (InputFit.No, "Das Spiel lebt vom Schwingen – mit Tasten kaum spielbar"),
            PlayStyle.WiiRemoteOnly => (InputFit.No, "Das Spiel nimmt nur Wii Remotes an – der Hub stellt die Tastatur als Pro Controller ein"),
            PlayStyle.Pointer => (InputFit.Limited, "Zeiger mit I/J/K/L – geht, ist aber hakelig"),
            PlayStyle.Nunchuk => (InputFit.Limited, "Spielbar; Schütteln liegt auf F/H"),
            PlayStyle.Mixed => (InputFit.Limited, "Je nach Disziplin werden GamePad oder Wii Remote verlangt"),
            PlayStyle.GamePadTouch => (InputFit.Works, "Touchscreen mit der Maus, Tasten auf der Tastatur"),
            PlayStyle.Handheld => (InputFit.Recommended, "Tastatur + Maus: Touchscreen mit der Maus direkt daneben"),
            PlayStyle.HandheldTouch => (InputFit.Recommended, "Wird fast nur per Touchscreen gespielt – Maus + Tastatur"),
            PlayStyle.HandheldAction => (InputFit.Works, "Geht, ein Controller ist hier angenehmer"),
            _ when backend == EmulatorIds.WiiCompiled => (InputFit.Works, "WiiCompiled: Tastatur direkt, Feinbelegung im Spiel mit F10"),
            _ => (InputFit.Works, "Geht – ein Controller ist aber angenehmer"),
        };
        return new InputAdvice(ControllerKind.Keyboard, "Tastatur", fit, why);
    }

    private static InputAdvice Pad(GameEntry g, PlayStyle style)
    {
        (InputFit fit, string why) = style switch
        {
            PlayStyle.MotionPlus => (InputFit.No, "Braucht Wii MotionPlus – Stick und Gyro reichen dafür nicht"),
            PlayStyle.Motion => (InputFit.Limited, "Schwingen per Stick-Klick – geht, macht aber wenig Spaß"),
            PlayStyle.WiiRemoteOnly => (InputFit.No, "Das Spiel unterstützt keinen Pro Controller – so stellt der Hub den PS5-Controller ein"),
            PlayStyle.Pointer => (InputFit.Limited, "Zeiger mit dem rechten Stick"),
            PlayStyle.Nunchuk => (InputFit.Works, "Wie Wii Remote + Nunchuk belegt, Schütteln per Stick-Klick"),
            PlayStyle.Mixed => (InputFit.Limited, "Pro Controller geht nicht in jeder Disziplin"),
            PlayStyle.GamePadTouch => (InputFit.Works, "Als Pro Controller; GamePad-Touchscreen mit der Maus (Strg+Tab)"),
            PlayStyle.Handheld => (InputFit.Works, "Tasten nach Position belegt; für den Touchscreen brauchst du trotzdem die Maus"),
            PlayStyle.HandheldTouch => (InputFit.Limited, "Das Spiel läuft fast nur über den Touchscreen – der Controller hilft kaum"),
            PlayStyle.HandheldAction => (InputFit.Recommended, "Schnelles Tastenspiel – Steuerkreuz/Stick und Schultertasten liegen besser"),
            _ when g.Platform == HubPlatform.GameCube => (InputFit.Recommended, "Als GameCube-Controller belegt"),
            _ when g.Special == SpecialPage.MarioKartWii => (InputFit.Recommended, "Mit GameCube-Belegung – die bequemste Art zu fahren"),
            _ when g.Platform == HubPlatform.Wii => (InputFit.Recommended, "Unterstützt Classic-/GameCube-Controller – der Hub belegt das passend"),
            _ when g.Platform == HubPlatform.WiiU => (InputFit.Recommended, "Als Wii U Pro Controller, inkl. Gyro"),
            _ when g.Platform == HubPlatform.Switch => (InputFit.Recommended, "Als Pro Controller, inkl. Gyro und Vibration"),
            _ => (InputFit.Recommended, "Direkt unterstützt"),
        };
        return new InputAdvice(ControllerKind.PlayStation5, "PS5", fit, why);
    }

    private static InputAdvice Wiimote(GameEntry g, string backend, PlayStyle style)
    {
        (InputFit fit, string why) = g.Platform switch
        {
            HubPlatform.GameCube => (InputFit.No, "GameCube-Spiele brauchen einen GameCube-Controller"),
            HubPlatform.DS or HubPlatform.ThreeDS => (InputFit.No, "Handheld-Emulatoren unterstützen keine Wii Remote"),
            HubPlatform.Switch => (InputFit.Limited, "Nur über PadForge als normales Gamepad – ohne Zeiger und Bewegung"),
            HubPlatform.Wii when backend == EmulatorIds.WiiCompiled => (InputFit.No, "WiiCompiled kennt keine Wii Remote – Engine auf Dolphin stellen"),
            HubPlatform.Wii => style switch
            {
                PlayStyle.MotionPlus => (InputFit.Recommended, "Braucht Wii Remote Plus oder den MotionPlus-Aufsatz"),
                PlayStyle.Motion or PlayStyle.Pointer or PlayStyle.Nunchuk => (InputFit.Recommended, "So war es gedacht – über die DolphinBar (Mode 4)"),
                _ when g.Special == SpecialPage.MarioKartWii => (InputFit.Works, "Quer als Lenkrad oder mit Nunchuk"),
                _ => (InputFit.Works, "Über die DolphinBar (Mode 4)"),
            },
            HubPlatform.WiiU => style switch
            {
                PlayStyle.WiiRemoteOnly => (InputFit.Recommended,
                    (g.Title.Contains("sports club", StringComparison.OrdinalIgnoreCase) ? "Braucht Wii Remote Plus. " : "") +
                    "In Cemu einmal einrichten: Controller-Profil → „Tastenbelegung automatisch setzen“ aus, dann in Cemu API „Wiimote“"),
                PlayStyle.Mixed => (InputFit.Limited, "Manche Disziplinen nutzen die Wii Remote – in Cemu von Hand einrichten"),
                PlayStyle.GamePadTouch => (InputFit.No, "Das Spiel wird über den GamePad-Touchscreen bedient"),
                _ when g.Title.Contains("breath of the wild", StringComparison.OrdinalIgnoreCase) => (InputFit.No, "Unterstützt keine Wii Remote"),
                _ => (InputFit.Limited, "Nur von Hand in Cemu (API „Wiimote“) und nicht in jedem Spiel"),
            },
            _ => (InputFit.No, "Nicht unterstützt"),
        };
        return new InputAdvice(ControllerKind.WiiRemote, "Wii Remote", fit, why);
    }

    public static string StyleName(PlayStyle s) => s switch
    {
        PlayStyle.Buttons => "Tastensteuerung",
        PlayStyle.Pointer => "Zeigersteuerung",
        PlayStyle.Nunchuk => "Wii Remote + Nunchuk",
        PlayStyle.Motion => "Bewegungssteuerung",
        PlayStyle.MotionPlus => "Bewegung mit MotionPlus",
        PlayStyle.WiiRemoteOnly => "nur Wii Remotes",
        PlayStyle.GamePadTouch => "GamePad-Touchscreen",
        PlayStyle.Mixed => "GamePad + Wii Remote",
        PlayStyle.Handheld => "Handheld (Touchscreen)",
        PlayStyle.HandheldAction => "Handheld (Tasten)",
        PlayStyle.HandheldTouch => "Handheld (nur Touch)",
        _ => "",
    };
}
