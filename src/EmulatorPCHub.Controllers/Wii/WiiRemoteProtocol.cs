namespace EmulatorPCHub.Controllers.Wii;

/// <summary>Ein Infrarot-Punkt, wie ihn die Kamera der Wii Remote sieht (X 0–1023, Y 0–767).</summary>
public readonly record struct IrDot(int X, int Y, int Size);

/// <summary>Inhalt eines Status-Reports (0x20).</summary>
public sealed record WiiRemoteStatus(int BatteryPercent, bool BatteryLow, bool ExtensionConnected, bool IrEnabled, int Leds);

/// <summary>Was an der Wii Remote steckt.</summary>
public enum WiiExtension { None, Unknown, Nunchuk, ClassicController, MotionPlus, MotionPlusNunchuk, MotionPlusClassic, Guitar, Drums, BalanceBoard }

/// <summary>Betriebsart der Mayflash DolphinBar (Mode-Taste, LED zeigt 1–4).</summary>
public enum DolphinBarMode
{
    /// <summary>Keine DolphinBar gefunden.</summary>
    None,
    /// <summary>Mode 1/2: Wii Remote steuert Maus/Tastatur.</summary>
    MouseKeyboard,
    /// <summary>Mode 3: Wii Remote wird Windows-Gamepad.</summary>
    Gamepad,
    /// <summary>Mode 4: Wii Remotes werden direkt an Dolphin durchgereicht (richtig für den Hub).</summary>
    Dolphin,
}

/// <summary>
/// Wii-Remote-Protokoll (wiibrew.org/wiki/Wiimote): Output-Reports bauen und Input-Reports lesen.
/// Reine Funktionen ohne Geräte-Zugriff – dadurch testbar.
/// </summary>
public static class WiiRemoteProtocol
{
    public const ushort NintendoVendor = 0x057E;
    public const ushort WiiRemoteProduct = 0x0306;
    /// <summary>Wii Remote Plus („-TR“, Motion Plus eingebaut).</summary>
    public const ushort WiiRemotePlusProduct = 0x0330;
    public const ushort MayflashVendor = 0x0079;

    public static bool IsWiiRemote(ushort vendor, ushort product) =>
        vendor == NintendoVendor && product is WiiRemoteProduct or WiiRemotePlusProduct;

    /// <summary>Ist der HID-Pfad (Kleinbuchstaben) ein Kandidat für Wii Remote/DolphinBar? Vermeidet das Öffnen fremder Geräte.</summary>
    public static bool IsCandidatePath(string path)
    {
        var p = path.ToLowerInvariant();
        return p.Contains("vid_057e") || p.Contains("vid&0002057e") || p.Contains("vid&0001057e") || p.Contains("vid_0079");
    }

    /// <summary>DolphinBar-Modus aus den HID-Geräten ableiten.</summary>
    public static DolphinBarMode DetectDolphinBar(IEnumerable<HidDeviceInfo> devices)
    {
        var list = devices.ToList();
        // Mode 4: Die DolphinBar meldet sich per USB als vier „Nintendo RVL-CNT-01“.
        if (list.Any(d => IsWiiRemote(d.Vendor, d.Product) && !d.Bluetooth))
            return DolphinBarMode.Dolphin;
        var mayflash = list.Where(d => d.Vendor == MayflashVendor
                                       && (d.ProductName?.Contains("Wiimote", StringComparison.OrdinalIgnoreCase) == true
                                           || d.Product is >= 0x1800 and <= 0x18FF)).ToList();
        if (mayflash.Count == 0)
            return DolphinBarMode.None;
        // Generic Desktop: 0x04 Joystick, 0x05 Gamepad → Mode 3; Maus (0x02)/Tastatur (0x06) → Mode 1/2
        return mayflash.Any(d => d.UsagePage == 0x01 && d.Usage is 0x04 or 0x05)
            ? DolphinBarMode.Gamepad
            : DolphinBarMode.MouseKeyboard;
    }

    /// <summary>Slot (1–4) eines DolphinBar-Geräts aus dem Pfad („&amp;col02“) – sonst 0.</summary>
    public static int SlotFromPath(string path)
    {
        var p = path.ToLowerInvariant();
        // DolphinBar Mode 4: je Slot eine USB-Schnittstelle „&mi_00“ … „&mi_03“
        var m = p.IndexOf("&mi_0", StringComparison.Ordinal);
        if (m >= 0 && m + 5 < p.Length && p[m + 5] is >= '0' and <= '3')
            return p[m + 5] - '0' + 1;
        var i = p.IndexOf("&col0", StringComparison.Ordinal);
        return i >= 0 && i + 5 < p.Length && p[i + 5] is >= '1' and <= '4' ? p[i + 5] - '0' : 0;
    }

    // ------------------------------------------------------------------
    // Output-Reports (Bit 0 des ersten Datenbytes = Vibration, hier immer aus außer beim Rütteln)
    // ------------------------------------------------------------------

    /// <summary>LEDs setzen: Bit 0 = LED 1 … Bit 3 = LED 4.</summary>
    public static byte[] SetLeds(int ledMask, bool rumble = false) => [0x11, (byte)(((ledMask & 0xF) << 4) | (rumble ? 1 : 0))];

    public static byte[] Rumble(bool on) => [0x10, (byte)(on ? 1 : 0)];

    public static byte[] RequestStatus() => [0x15, 0x00];

    /// <summary>Berichtsmodus setzen (z. B. 0x30 nur Tasten, 0x33 Tasten + Beschleunigung + IR).</summary>
    public static byte[] SetReportMode(byte mode, bool continuous) => [0x12, (byte)(continuous ? 0x04 : 0x00), mode];

    public static byte[] IrCamera(bool on) => [0x13, (byte)(on ? 0x04 : 0x00)];

    public static byte[] IrCamera2(bool on) => [0x1A, (byte)(on ? 0x04 : 0x00)];

    /// <summary>In ein Register schreiben (0x16, Adressraum 0x04 = Register).</summary>
    public static byte[] WriteRegister(int address, params byte[] data)
    {
        if (data.Length is 0 or > 16)
            throw new ArgumentOutOfRangeException(nameof(data));
        var r = new byte[22];
        r[0] = 0x16;
        r[1] = 0x04;
        r[2] = (byte)(address >> 16);
        r[3] = (byte)(address >> 8);
        r[4] = (byte)address;
        r[5] = (byte)data.Length;
        data.CopyTo(r, 6);
        return r;
    }

    /// <summary>Register lesen (0x17) – Antwort kommt als Report 0x21.</summary>
    public static byte[] ReadRegister(int address, int size) =>
        [0x17, 0x04, (byte)(address >> 16), (byte)(address >> 8), (byte)address, (byte)(size >> 8), (byte)size];

    /// <summary>
    /// IR-Kamera einschalten (Empfindlichkeitsstufe 3 wie die Wii, erweiterter Modus = 12 Byte IR in Report 0x33).
    /// Reihenfolge laut wiibrew: 0x13/0x1A, 0x08 → 0xB00030, Empfindlichkeit, Modus, 0x08 → 0xB00030.
    /// </summary>
    public static IEnumerable<byte[]> IrInitSequence()
    {
        yield return IrCamera(true);
        yield return IrCamera2(true);
        yield return WriteRegister(0xB00030, 0x08);
        yield return WriteRegister(0xB00000, 0x02, 0x00, 0x00, 0x71, 0x01, 0x00, 0xAA, 0x00, 0x64);
        yield return WriteRegister(0xB0001A, 0x63, 0x03);
        yield return WriteRegister(0xB00033, 0x03);
        yield return WriteRegister(0xB00030, 0x08);
        yield return SetReportMode(0x33, continuous: true);
    }

    /// <summary>Erweiterung ohne Verschlüsselung initialisieren (danach ID bei 0xA400FA lesen).</summary>
    public static IEnumerable<byte[]> ExtensionInitSequence()
    {
        yield return WriteRegister(0xA400F0, 0x55);
        yield return WriteRegister(0xA400FB, 0x00);
    }

    // ------------------------------------------------------------------
    // Input-Reports
    // ------------------------------------------------------------------

    /// <summary>Tasten aus den zwei Kernbytes (bei allen Reports 0x20–0x3F außer 0x3D an Byte 1–2).</summary>
    public static IReadOnlyList<string> Buttons(byte b1, byte b2)
    {
        var list = new List<string>();
        if ((b1 & 0x08) != 0) list.Add("↑");
        if ((b1 & 0x04) != 0) list.Add("↓");
        if ((b1 & 0x01) != 0) list.Add("←");
        if ((b1 & 0x02) != 0) list.Add("→");
        if ((b2 & 0x08) != 0) list.Add("A");
        if ((b2 & 0x04) != 0) list.Add("B");
        if ((b2 & 0x02) != 0) list.Add("1");
        if ((b2 & 0x01) != 0) list.Add("2");
        if ((b1 & 0x10) != 0) list.Add("+");
        if ((b2 & 0x10) != 0) list.Add("−");
        if ((b2 & 0x80) != 0) list.Add("Home");
        return list;
    }

    public static WiiRemoteStatus? ParseStatus(ReadOnlySpan<byte> r)
    {
        if (r.Length < 7 || r[0] != 0x20)
            return null;
        var flags = r[3];
        // Volle Batterie liegt bei ca. 0xC8 (200)
        var percent = Math.Clamp(r[6] * 100 / 0xC8, 0, 100);
        return new WiiRemoteStatus(percent, (flags & 0x01) != 0, (flags & 0x02) != 0, (flags & 0x08) != 0, flags >> 4);
    }

    /// <summary>12 Byte „erweitertes“ IR-Format: 4 Punkte à 3 Byte (0xFF… = kein Punkt).</summary>
    public static List<IrDot> ParseExtendedIr(ReadOnlySpan<byte> ir)
    {
        var dots = new List<IrDot>(4);
        for (var i = 0; i + 2 < ir.Length && i < 12; i += 3)
        {
            if (ir[i] == 0xFF && ir[i + 1] == 0xFF && ir[i + 2] == 0xFF)
                continue;
            var x = ir[i] | ((ir[i + 2] >> 4) & 0x03) << 8;
            var y = ir[i + 1] | ((ir[i + 2] >> 6) & 0x03) << 8;
            if (x >= 1023 && y >= 1023)
                continue;
            dots.Add(new IrDot(x, y, ir[i + 2] & 0x0F));
        }
        return dots;
    }

    /// <summary>Beschleunigung (Rohwerte, ca. 0x80 = 0 g) aus Report 0x31/0x33/0x35/0x37.</summary>
    public static (int X, int Y, int Z) ParseAccel(ReadOnlySpan<byte> r) =>
        r.Length >= 6 ? ((r[3] << 2) | ((r[1] >> 5) & 0x03), (r[4] << 2) | ((r[2] >> 4) & 0x02), (r[5] << 2) | ((r[2] >> 5) & 0x02)) : (512, 512, 512);

    /// <summary>Erweiterungs-ID (6 Byte bei 0xA400FA) → Typ.</summary>
    public static WiiExtension IdentifyExtension(ReadOnlySpan<byte> id)
    {
        if (id.Length < 6 || id.SequenceEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }))
            return WiiExtension.Unknown;
        var value = ((long)id[0] << 40) | ((long)id[1] << 32) | ((long)id[2] << 24) | ((long)id[3] << 16) | ((long)id[4] << 8) | id[5];
        return value switch
        {
            0x0000A4200000 or 0xFF00A4200000 => WiiExtension.Nunchuk,
            0x0000A4200101 or 0x0100A4200101 => WiiExtension.ClassicController,
            0x0000A4200103 => WiiExtension.Guitar,
            0x0100A4200103 => WiiExtension.Drums,
            0x0000A4200402 => WiiExtension.BalanceBoard,
            0x0000A4200405 or 0x0100A4200405 => WiiExtension.MotionPlus,
            0x0000A4200505 => WiiExtension.MotionPlusNunchuk,
            0x0000A4200705 => WiiExtension.MotionPlusClassic,
            _ => WiiExtension.Unknown,
        };
    }

    public static string DisplayName(this WiiExtension e) => e switch
    {
        WiiExtension.None => "keine",
        WiiExtension.Nunchuk => "Nunchuk",
        WiiExtension.ClassicController => "Classic Controller",
        WiiExtension.MotionPlus => "MotionPlus",
        WiiExtension.MotionPlusNunchuk => "MotionPlus + Nunchuk",
        WiiExtension.MotionPlusClassic => "MotionPlus + Classic Controller",
        WiiExtension.Guitar => "Gitarre",
        WiiExtension.Drums => "Schlagzeug",
        WiiExtension.BalanceBoard => "Balance Board",
        _ => "Erweiterung",
    };

    /// <summary>LED-Maske für Spieler 1–4 (wie Wii und Dolphin: eine LED an Position n).</summary>
    public static int LedsForPlayer(int player) => player is >= 1 and <= 4 ? 1 << (player - 1) : 0;

    /// <summary>Spieler aus der LED-Maske, falls genau eine LED leuchtet (sonst 0).</summary>
    public static int PlayerFromLeds(int leds) => leds switch { 1 => 1, 2 => 2, 4 => 3, 8 => 4, _ => 0 };

    /// <summary>
    /// Zeigerposition 0–1 aus den zwei hellsten Punkten (Mitte, gespiegelt – die Kamera sieht die Leiste „andersherum“).
    /// Null, wenn weniger als zwei Punkte sichtbar sind.
    /// </summary>
    public static (double X, double Y)? Pointer(IReadOnlyList<IrDot> dots)
    {
        if (dots.Count < 2)
            return null;
        var pair = dots.OrderByDescending(d => d.Size).Take(2).ToList();
        var mx = (pair[0].X + pair[1].X) / 2.0;
        var my = (pair[0].Y + pair[1].Y) / 2.0;
        return (Math.Clamp(1 - mx / 1023.0, 0, 1), Math.Clamp(my / 767.0, 0, 1));
    }
}
