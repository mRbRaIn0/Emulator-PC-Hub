namespace EmulatorPCHub.Core;

/// <summary>Deutsch → Englisch (Teil 3: Mario-Kart-Seiten, Mii, Mods, Einrichtung, Dienste, Vorlagen mit Platzhaltern).</summary>
internal static partial class Entries
{
    private static void AddGamePages(Action<string, string> A)
    {
        // ── Mario Kart 8 Deluxe ────────────────────────────────────
        A("Edition", "Edition"); A("Eigene Mod-Konfiguration", "Custom mod configuration");
        A("Noch keine Mods vorhanden. Mods-Seite → Mod importieren.", "No mods yet. Mods page → Import mod.");
        A("14 zusätzliche Cups · 56 Custom Tracks · neuer Soundtrack", "14 additional cups · 56 custom tracks · new soundtrack");
        A("nicht installiert", "not installed"); A("Einrichtung", "Setup"); A("Anderen Dump wählen", "Choose another dump");
        A("Eigenen Dump wählen", "Choose your own dump"); A("DLC & Updates eintragen", "Register DLC & updates");
        A("CTGP Deluxe importieren (ZIP)", "Import CTGP Deluxe (ZIP)"); A("Switch-Emulator einrichten (Keys, Firmware)", "Set up Switch emulator (keys, firmware)");
        A("Switch-Emulator-Ordner", "Switch emulator folder"); A("Eigene Spielkopie, Keys, Firmware & DLCs", "Your own game copy, keys, firmware & DLC");
        A("Der Hub lädt keine Spiele, Keys, Firmware oder DLCs herunter. Nutze deine eigenen Dumps: ", "The hub does not download games, keys, firmware or DLC. Use your own dumps: ");
        A("Spiel (NSP/XCI) und DLCs/Updates (NSP) in den Switch-Bibliotheksordner legen – der Hub erkennt ", "put the game (NSP/XCI) and DLC/updates (NSP) in the Switch library folder – the hub recognizes ");
        A("Updates und DLCs (z. B. Booster-Streckenpass) automatisch und trägt sie im Emulator ein. ", "updates and DLC (e.g. Booster Course Pass) automatically and registers them in the emulator. ");
        A("prod.keys gehört nach …\\user\\keys\\, die Firmware wird in Eden über Tools → Install Firmware eingespielt.", "prod.keys belongs in …\\user\\keys\\, the firmware is installed in Eden via Tools → Install Firmware.");
        A("Eden ist noch nicht installiert. Lade Eden selbst von der offiziellen Seite ", "Eden is not installed yet. Download Eden yourself from the official site ");
        A("(git.eden-emu.dev) und installiere es unter Komponenten → Eden → „Aus Datei installieren …“ (Datei ", "(git.eden-emu.dev) and install it under Components → Eden → “Install from file …” (file ");
        A("Switch-Emulator einrichten", "Set up Switch emulator"); A("Datei nicht passend", "File does not match");
        A("CTGP Deluxe importiert ✓", "CTGP Deluxe imported ✓");
        A("Der Switch-Emulator öffnet sich jetzt einmal normal. Dort:\n\n", "The Switch emulator now opens once as usual. There:\n\n");
        A("1. Eigene prod.keys einspielen (Tools → Install Decryption Keys)\n", "1. Install your own prod.keys (Tools → Install Decryption Keys)\n");
        A("2. Eigene Firmware installieren (Tools → Install Firmware)\n\n", "2. Install your own firmware (Tools → Install Firmware)\n\n");
        A("Danach das Fenster schließen – der Hub trägt dann Spieleordner, Updates/DLCs und die Controller-Belegung selbst ein.", "Then close the window – the hub then registers game folders, updates/DLC and the controller mapping itself.");

        // ── Mario Kart Wii ─────────────────────────────────────────
        A("Kein Controller – Tastatur", "No controller – keyboard"); A("Engine", "Engine");
        A("Mit Dolphin schreibt der Hub die Belegung automatisch (Profil pro Spiel). WiiCompiled: Feinbelegung im Spiel mit F10.", "With Dolphin the hub writes the mapping automatically (profile per game). WiiCompiled: fine mapping in game with F10.");
        A("Controller-Profil für Mario Kart Wii", "Controller profile for Mario Kart Wii"); A("Strecken", "Tracks");
        A("WiiCompiled installieren", "Install WiiCompiled"); A("Retro Rewind installieren", "Install Retro Rewind");
        A("Retro Rewind aktualisieren", "Update Retro Rewind"); A("Retro-Rewind-Webseite", "Retro Rewind website");
        A("Advanced Tools", "Advanced tools"); A("Wheel Wizard ist nicht installiert (Komponenten).", "Wheel Wizard is not installed (Components).");
        A("Dolphin-Ordner", "Dolphin folder");
        A("Der normale Weg ist: Hub → Mario Kart Wii → Spielen. Wheel Wizard wird nur für Sonderfälle gebraucht.", "The normal way is: hub → Mario Kart Wii → Play. Wheel Wizard is only needed for special cases.");
        A("Eigene Spielkopie", "Your own game copy");
        A("Der Hub verteilt keine Spiele. Lege deinen eigenen Mario-Kart-Wii-Dump (ISO/WBFS/RVZ) in einen ", "The hub does not distribute games. Put your own Mario Kart Wii dump (ISO/WBFS/RVZ) in a ");
        A("Bibliotheksordner oder wähle ihn oben aus. WiiCompiled braucht die PAL-Version (RMCP01), ", "library folder or select it above. WiiCompiled needs the PAL version (RMCP01), ");
        A("Dolphin funktioniert mit allen Regionen.", "Dolphin works with all regions.");
        A("Dump nicht erkannt", "Dump not recognized"); A("Mario-Kart-Wii-Dump übernommen ✓", "Mario Kart Wii dump applied ✓");
        A("WiiCompiled übersetzt deinen eigenen Mario-Kart-Wii-Dump in ein natives PC-Programm (inkl. Retro Rewind, falls installiert).\n\n", "WiiCompiled translates your own Mario Kart Wii dump into a native PC program (including Retro Rewind if installed).\n\n");
        A("Das dauert mehrere Minuten und braucht vorübergehend ca. 20 GB freien Speicher. Fortfahren?", "This takes several minutes and temporarily needs about 20 GB of free space. Continue?");
        A("WiiCompiled ist installiert ✓", "WiiCompiled is installed ✓");
        A("Der Hub lädt keine Mods herunter. Lade das vollständige Retro-Rewind-Paket (ZIP) selbst von der ", "The hub does not download mods. Download the complete Retro Rewind package (ZIP) yourself from the ");
        A("offiziellen Seite herunter und wähle es danach aus – es wird in den Dolphin-Userordner entpackt.", "official site and select it afterwards – it is extracted into the Dolphin user folder.");
        A("ZIP wählen …", "Choose ZIP …");
        A("Retro Rewind ist nicht installiert. Vanilla enthält die 32 Original-Strecken.", "Retro Rewind is not installed. Vanilla contains the 32 original tracks.");
        A("Tracks", "Tracks"); A("Strecken – Retro Rewind ", "Tracks – Retro Rewind ");
        A("Eine Streckenliste mit Cup-Übersicht, Suche und Favoriten ist als spätere Erweiterung vorgesehen (Plan Abschnitt 7).", "A track list with cup overview, search and favorites is planned as a later extension (plan section 7).");
        A("Retro Rewind: My-Stuff-Mods laden", "Retro Rewind: load My Stuff mods");
        A("Retro Rewind: getrennter Spielstand (Dolphin)", "Retro Rewind: separate save (Dolphin)");
        A("Spiel im Vollbild starten", "Start game in fullscreen");
        A("Diese Einstellungen gelten für den Start mit Dolphin. WiiCompiled speichert seine Optionen selbst (Config.toml).", "These settings apply to launching with Dolphin. WiiCompiled stores its options itself (Config.toml).");
        A("Mario Kart Wii – Einstellungen", "Mario Kart Wii – settings"); A("Retro-Rewind-Version wird geprüft …", "Checking Retro Rewind version …");

        A("Tastatur / Controller", "Keyboard / controller"); A("Kein eigener Mario-Kart-Wii-Dump gefunden", "No own Mario Kart Wii dump found");
        A("WiiCompiled bereit zur Installation", "WiiCompiled ready to install"); A("WiiCompiled: ", "WiiCompiled: ");
        A("WiiCompiled nicht heruntergeladen", "WiiCompiled not downloaded"); A("Dolphin nicht installiert", "Dolphin not installed");
        A("Wheel Wizard nicht installiert", "Wheel Wizard not installed"); A("Nativ auf dem PC (empfohlen)", "Native on PC (recommended)");
        A("Emulation (alle Regionen, Fallback)", "Emulation (all regions, fallback)"); A("Eigenen Dump auswählen, um zu spielen", "Select your own dump to play");
        A("WiiCompiled zuerst installieren (oder Engine Dolphin wählen)", "Install WiiCompiled first (or choose the Dolphin engine)");
        A("Eigenen PAL-Dump hinzufügen, dann WiiCompiled installieren", "Add your own PAL dump, then install WiiCompiled");
        A("Dolphin unter Komponenten installieren", "Install Dolphin under Components");
        A("Diese Datei ist kein Mario-Kart-Wii-Image (erwartet Disc-ID RMC…).", "This file is not a Mario Kart Wii image (expected disc ID RMC…).");
        A("WiiCompiled wird installiert – das kann eine Weile dauern …", "Installing WiiCompiled – this may take a while …");
        A("Retro Rewind wird installiert …", "Installing Retro Rewind …");
        A("Kein eigener Mario-Kart-8-Deluxe-Dump (NSP/XCI) gefunden", "No own Mario Kart 8 Deluxe dump (NSP/XCI) found");
        A("Keys vorhanden ✓", "Keys present ✓"); A("Keys fehlen – eigene prod.keys aus deiner Switch nötig", "Keys missing – your own prod.keys from your Switch are required");
        A("Firmware installiert ✓", "Firmware installed ✓"); A("Firmware fehlt – eigene Firmware in Eden installieren (Tools → Install Firmware)", "Firmware missing – install your own firmware in Eden (Tools → Install Firmware)");
        A("CTGP Deluxe aktiv", "CTGP Deluxe active"); A("CTGP Deluxe bereit", "CTGP Deluxe ready"); A("CTGP Deluxe nicht installiert", "CTGP Deluxe not installed");
        A("Keine eigenen DLC-Dateien gefunden", "No own DLC files found");
        A("Kein Update gefunden (CTGP Deluxe braucht Version 3.0.3)", "No update found (CTGP Deluxe needs version 3.0.3)");
        A("Spielversion passt zu CTGP Deluxe ✓", "Game version matches CTGP Deluxe ✓"); A("CTGP Deluxe installieren", "Install CTGP Deluxe");
        A("Switch-Emulator unter Komponenten installieren", "Install the Switch emulator under Components");
        A("Eigenen Dump hinzufügen, um zu spielen", "Add your own dump to play"); A("Eigene Keys fehlen (prod.keys)", "Your own keys are missing (prod.keys)");
        A("Status: Bereit", "Status: Ready"); A("CTGP Deluxe wird importiert …", "Importing CTGP Deluxe …");
        A("Keine eigenen Update-/DLC-Dateien gefunden.", "No own update/DLC files found.");

        // ── Mii ────────────────────────────────────────────────────
        A("Deine Miis aus Wii und Wii U an einem Ort. Ein Mii auswählen → einem Profil oder einer Konsole zuweisen. ", "Your Miis from Wii and Wii U in one place. Select a Mii → assign it to a profile or a console. ");
        A("Miis werden im nativen Format gespeichert und unverändert übernommen.", "Miis are stored in their native format and taken over unchanged.");
        A("Neues Mii erstellen …", "Create new Mii …"); A("Aus Dolphin (Wii) importieren", "Import from Dolphin (Wii)");
        A("Aus Cemu (Wii U) importieren", "Import from Cemu (Wii U)"); A("Datei importieren …", "Import file …");
        A("Noch keine Miis. Importiere sie aus Dolphin/Cemu oder erstelle eins im Mii-Editor deiner Konsole.", "No Miis yet. Import them from Dolphin/Cemu or create one in your console's Mii editor.");
        A("Zuweisen …", "Assign …"); A("Mii umbenennen", "Rename Mii"); A("Name (max. 10 Zeichen)", "Name (max. 10 characters)");
        A("Exportieren …", "Export …"); A("Exportiert: ", "Exported: "); A("Mii löschen", "Delete Mii"); A("Hub-Profil …", "Hub profile …");
        A("Wii (Dolphin – Mii-Datenbank)", "Wii (Dolphin – Mii database)"); A("Wii U (Cemu-Konto) …", "Wii U (Cemu account) …");
        A("Switch (Eden – Mii-Datenbank)", "Switch (Eden – Mii database)"); A("Bitte zuerst das laufende Spiel beenden.", "Please quit the running game first.");
        A("Switch (Eden-Benutzer benennen) …", "Switch (name Eden user) …");
        A("umgewandelt; Wii-U-/3DS-Miis gehen nicht zurück auf die Wii.", "converted; Wii U/3DS Miis cannot go back to the Wii.");
        A("Mii zuweisen", "Assign Mii"); A("Mii-Zuweisung fehlgeschlagen", "Mii assignment failed"); A("Fehlgeschlagen: ", "Failed: ");
        A("Profil wählen", "Choose profile"); A("Cemu hat noch keine Konten (Cemu einmal starten).", "Cemu has no accounts yet (start Cemu once).");
        A("Cemu-Konto wählen", "Choose Cemu account"); A("Eden hat noch keine Benutzer (Eden einmal starten).", "Eden has no users yet (start Eden once).");
        A("Eden-Benutzer wählen", "Choose Eden user"); A("Mii-Import", "Mii import");
        A("Wii – Mii-Kanal (Dolphin)", "Wii – Mii Channel (Dolphin)"); A("Dolphin ist nicht installiert.", "Dolphin is not installed.");
        A("Der Mii-Kanal fehlt in Dolphin. Ihn aus deiner eigenen Wii in Dolphins NAND übernehmen (Extras → NAND-Verwaltung / Wii-Menü installieren).", "The Mii Channel is missing in Dolphin. Take it from your own Wii into Dolphin's NAND (Tools → NAND management / install Wii Menu).");
        A("Wii U – Mii Maker (Cemu)", "Wii U – Mii Maker (Cemu)"); A("Cemu ist nicht installiert.", "Cemu is not installed.");
        A("Der Mii Maker fehlt in Cemu (Systemtitel aus deiner eigenen Wii U in die mlc01 übernehmen). Alternativ ein Cemu-Konto anlegen – Cemu erstellt dafür ein Standard-Mii.", "The Mii Maker is missing in Cemu (copy system titles from your own Wii U into mlc01). Alternatively create a Cemu account – Cemu creates a default Mii for it.");
        A("Switch – Mii-Editor (Eden)", "Switch – Mii editor (Eden)"); A("Eden ist nicht installiert.", "Eden is not installed.");
        A("Neues Mii erstellen", "Create new Mii"); A("   (nicht verfügbar)", "   (not available)");
        A("Miis werden im Original-Editor der Konsole erstellt (mit deinen eigenen System-Dumps). Nach dem Schließen übernimmt der Hub neue Miis automatisch.", "Miis are created in the console's original editor (with your own system dumps). After closing it the hub imports new Miis automatically.");
        A("Eden öffnet sich jetzt. Den Mii-Editor findest du dort unter Tools → Open Mii Editor ", "Eden opens now. You will find the Mii editor there under Tools → Open Mii Editor ");
        A("(braucht deine installierte Firmware). Danach Eden schließen.", "(needs your installed firmware). Then close Eden.");
        A("Mii-Editor konnte nicht gestartet werden", "Mii editor could not be started"); A("Start fehlgeschlagen: ", "Start failed: ");
        A("Nur Wii-Miis können in die Wii übernommen werden. Wii-U-/3DS-Miis bitte im Mii-Kanal neu anlegen.", "Only Wii Miis can be transferred to the Wii. Please recreate Wii U/3DS Miis in the Mii Channel.");
        A("Die Wii-Mii-Datenbank ist voll (100 Miis).", "The Wii Mii database is full (100 Miis)."); A("Die Switch-Mii-Datenbank ist voll.", "The Switch Mii database is full.");

        // ── Mods ───────────────────────────────────────────────────
        A("Retro Rewind nicht installiert", "Retro Rewind not installed"); A("My-Stuff-Mods (eigene Mods in Retro Rewind)", "My Stuff mods (own mods in Retro Rewind)");
        A(" (Wheel Wizard)", " (Wheel Wizard)"); A("Mario-Kart-Wii-Seite", "Mario Kart Wii page"); A("Mods in Wheel Wizard verwalten", "Manage mods in Wheel Wizard");
        A("Keine Mods vorhanden.", "No mods available.");
        A("Beim Start setzt das gewählte Preset die Mods automatisch: Vanilla = keine Mods, CTGP Deluxe = nur CTGP, Custom = deine Auswahl.", "At launch the chosen preset sets the mods automatically: Vanilla = no mods, CTGP Deluxe = CTGP only, Custom = your selection.");
        A("Mod importieren (ZIP)", "Import mod (ZIP)"); A("Mod importieren (Ordner)", "Import mod (folder)"); A("Mod-Speicher öffnen", "Open mod storage");
        A("Cemu – Graphic Packs", "Cemu – graphic packs");
        A("Noch keine Graphic Packs. In Cemu: Optionen → Graphic Packs → „Download latest community graphic packs“.", "No graphic packs yet. In Cemu: Options → Graphic packs → “Download latest community graphic packs”.");
        A("Backups (Restore Backup)", "Backups (restore backup)");
        A("Noch keine Backups. Vor Änderungen an Mods, Configs und Presets wird automatisch gesichert.", "No backups yet. Mods, configs and presets are backed up automatically before changes.");
        A("Mod", "Mod"); A("Mod wird importiert …", "Importing mod …"); A("Mod-Import", "Mod import"); A("Backup wiederherstellen", "Restore backup");
        A("Backup wiederhergestellt ✓", "Backup restored ✓"); A("Wiederherstellung fehlgeschlagen (siehe Logs)", "Restore failed (see logs)");

        // ── Einrichtungs-Assistent ─────────────────────────────────
        A("Spieleordner auswählen", "Choose game folders"); A("Emulatoren erkennen", "Detect emulators"); A("Fehlende Komponenten", "Missing components");
        A("Controller erkennen", "Detect controllers"); A("Pfade prüfen", "Check paths"); A("Spiele importieren", "Import games"); A("Startmenü", "Start menu");
        A("Fertig – zum Hub", "Done – to the hub");
        A("Wähle die Ordner mit deinen eigenen Spiele-Dumps. Spiele müssen nicht verschoben werden.", "Choose the folders with your own game dumps. Games do not have to be moved.");
        A("Noch kein Ordner", "No folder yet"); A("Gefundene Ordner", "Folders found"); A("Wheel Wizard gefunden", "Wheel Wizard found");
        A("Wheel Wizard nicht gefunden", "Wheel Wizard not found"); A("Alle Komponenten sind installiert.", "All components are installed.");
        A("Kein Controller gefunden – Tastatur funktioniert immer (Pfeile, Enter, Esc).", "No controller found – the keyboard always works (arrows, Enter, Esc).");
        A("Controller verwalten", "Manage controllers"); A("Hub-Stammordner: ", "Hub root folder: "); A("Dolphin-Userordner: ", "Dolphin user folder: ");
        A("Wheel Wizard → Dolphin: ", "Wheel Wizard → Dolphin: "); A("nicht verbunden", "not connected");
        A("Wheel Wizard mit Hub verbinden", "Connect Wheel Wizard to hub"); A("Spiele werden gesucht …", "Searching for games …");
        A("Erneut scannen", "Scan again"); A("\nMario Kart Wii erkannt ✓", "\nMario Kart Wii detected ✓"); A("\nMario Kart 8 Deluxe erkannt ✓", "\nMario Kart 8 Deluxe detected ✓");
        A("Cover und Hintergründe werden lokal verwaltet – der Hub liefert keine fremden Bilder mit.", "Covers and backgrounds are managed locally – the hub ships no third-party images.");
        A("Möglichkeiten:\n• Auf der Spielseite „Cover wählen“\n• Bild mit gleichem Namen neben die Spieldatei legen (z. B. Spiel.png)\n", "Options:\n• “Choose cover” on the game page\n• Put an image with the same name next to the game file (e.g. Game.png)\n");
        A("• data/artwork/<Spiel-ID>/cover.png und background.png\nOhne Cover erzeugt der Hub eigene Kacheln.", "• data/artwork/<game ID>/cover.png and background.png\nWithout a cover the hub generates its own tiles.");
        A("Artwork-Ordner öffnen", "Open artwork folder");
        A("Fast fertig! Soll der Hub im Startmenü erscheinen oder direkt mit Windows starten?", "Almost done! Should the hub appear in the Start menu or start with Windows?");
        A("Startmenü-Verknüpfung erstellen", "Create Start menu shortcut"); A("Verknüpfung erstellt ✓", "Shortcut created ✓");
        A("Verknüpfung konnte nicht erstellt werden", "Shortcut could not be created"); A("Mit Windows starten (Console Mode)", "Start with Windows (Console Mode)");

        // ── Meldungen der Dienste ──────────────────────────────────
        A("Home + Minus: laufendes Spiel wird beendet", "Home + Minus: quitting running game"); A("Spiel konnte nicht gestartet werden", "Game could not be started");
        A("Vor dem Spielen", "Before playing"); A("Nach dem Spielen", "After playing"); A("Vor Wiederherstellen", "Before restore");
        A("Der Switch-Emulator ist nicht installiert (Komponenten → Eden).", "The Switch emulator is not installed (Components → Eden).");
        A("Switch-Einrichtung unvollständig", "Switch setup incomplete"); A("Switch-Emulator eingerichtet ✓ Keys und Firmware vorhanden.", "Switch emulator set up ✓ Keys and firmware present.");
        A("Keys vorhanden – jetzt noch die eigene Firmware im Switch-Emulator installieren (Eden: Tools → Install Firmware).", "Keys present – now install your own firmware in the Switch emulator (Eden: Tools → Install Firmware).");
        A("Ersteinrichtung nötig: einmal starten (Controller-Seite → PadForge einrichten), installiert den HIDMaestro-Treiber.", "Initial setup required: start once (Controllers page → Set up PadForge), installs the HIDMaestro driver.");
        A("Setup liegt bereit – Installation braucht deinen Mario-Kart-Wii-PAL-Dump (RMCP01).", "Setup is ready – installation needs your Mario Kart Wii PAL dump (RMCP01).");
        A("Dolphin – Wii-Spielstand (für alle Profile)", "Dolphin – Wii save (for all profiles)");
        A("Dolphin – GameCube-Memory-Cards (gemeinsam für alle GameCube-Spiele)", "Dolphin – GameCube memory cards (shared by all GameCube games)");
        A("Cemu – gemeinsame Daten (alle Konten)", "Cemu – shared data (all accounts)"); A("melonDS – Spielstand (für alle Profile)", "melonDS – save (for all profiles)");
        A("Azahar – Spielstand auf der emulierten SD-Karte (für alle Profile)", "Azahar – save on the emulated SD card (for all profiles)");
        A("Eingebautes Spiel ohne Spielstand-Ordner.", "Built-in game without a save folder.");
        A("Keine Spiel-ID erkannt – der Spielstand-Ordner lässt sich nicht bestimmen.", "No game ID detected – the save folder cannot be determined.");
        A("Ungültiger Pfad im Snapshot.", "Invalid path in snapshot."); A("Die Datei ist keine Spielstand-Sicherung des Hubs.", "The file is not a hub save backup.");
        A("Import: ", "Import: ");
        A("Dolphin läuft gerade – bitte zuerst beenden, sonst überschreibt Dolphin die Einstellungen.", "Dolphin is currently running – please quit it first, otherwise Dolphin overwrites the settings.");
        A("Wii-Systemeinstellungen (SYSCONF) fehlen – einmal ein Wii-Spiel in Dolphin starten, dann erneut versuchen.", "Wii system settings (SYSCONF) are missing – start a Wii game in Dolphin once, then try again.");
        A("Controller-Konfiguration konnte nicht geschrieben werden", "Controller configuration could not be written");
        A("Kompatibilitätsschicht (PadForge) wird gestartet …", "Starting compatibility layer (PadForge) …");
        A("PadForge nicht erreichbar – Controller werden direkt übergeben", "PadForge not reachable – passing controllers directly");
        A("PadForge (virtueller Xbox-Controller)", "PadForge (virtual Xbox controller)");
        A(" – PadForge ist nicht installiert/aktiv, Gerät wird direkt übergeben", " – PadForge is not installed/active, device is passed directly");
        A("Aktion: sprechen, aufheben, öffnen", "Action: talk, pick up, open"); A("Sprinten / abbrechen", "Sprint / cancel"); A("Springen", "Jump");
        A("Angreifen", "Attack"); A("Bogen spannen / schießen", "Draw bow / shoot"); A("Anvisieren / Schild", "Target / shield"); A("Waffe werfen", "Throw weapon");
        A("Shiekah-Stein-Modul benutzen", "Use Sheikah Slate rune"); A("Menü (Inventar)", "Menu (inventory)"); A("Karte", "Map"); A("Laufen", "Walk");
        A("Kamera", "Camera"); A("Schleichen", "Sneak"); A("Fernrohr", "Scope"); A("Module auswählen", "Select runes"); A("Pfeifen (Pferd rufen)", "Whistle (call horse)");
        A("Schild wechseln", "Switch shield"); A("Waffe wechseln", "Switch weapon"); A("Gas geben", "Accelerate"); A("Bremsen / rückwärts", "Brake / reverse");
        A("Springen / driften", "Jump / drift"); A("Item benutzen", "Use item"); A("Nach hinten schauen", "Look back"); A("Pause", "Pause"); A("Lenken", "Steer");
        A("Springen / driften / bremsen", "Jump / drift / brake"); A("Trick (beim Sprung)", "Trick (while jumping)");
        A("Angreifen", "Attack");
    }

    /// <summary>Texte mit Platzhaltern {0}, {1} … (bei Interpolation im Quelltext); Platzhalter werden selbst übersetzt.</summary>
    public static readonly (string de, string en)[] Templates =
    [
        ("Willkommen zurück! {0}: {1} Min. gespielt", "Welcome back! {0}: played {1} min"),
        ("{0} verbunden → Spieler {1}", "{0} connected → player {1}"),
        ("{0} verbunden (kein freier Spieler-Platz)", "{0} connected (no free player slot)"),
        ("{0} getrennt", "{0} disconnected"),
        ("🔋 Akku schwach: {0} (Spieler {1}) – {2} %", "🔋 Low battery: {0} (player {1}) – {2} %"),
        ("Spieler {0}", "Player {0}"),
        ("Spieler {0}: {1}", "Player {0}: {1}"),
        ("Slot {0}", "Slot {0}"),
        ("Dein Controller ({0}) – im PS5-Schema dargestellt", "Your controller ({0}) – shown in PS5 layout"),
        ("Im Spiel ({0})", "In game ({0})"),
        ("Empfohlen: {0}", "Recommended: {0}"),
        ("   ·   Spielart: {0}", "   ·   Game type: {0}"),
        ("Lichtleiste {0}", "Light bar {0}"),
        ("LED {0} leuchtet grün", "LED {0} glows green"),
        ("LED {0} leuchtet blau", "LED {0} glows blue"),
        ("Spieler {0} · LED {1} leuchtet blau", "Player {0} · LED {1} glows blue"),
        ("Autostart {0}", "Autostart {0}"),
        ("Controller – {0}", "Controllers – {0}"),
        ("Eigenes Profil · Emulator: {0}", "Own profile · emulator: {0}"),
        ("Nutzt das Standardprofil · Emulator: {0}", "Uses the default profile · emulator: {0}"),
        ("Automatisch: wer auf Platz {0} ist (jetzt: {1})", "Automatic: whoever is in slot {0} (currently: {1})"),
        ("Automatisch: wer auf Platz {0} ist (jetzt: niemand)", "Automatic: whoever is in slot {0} (currently: nobody)"),
        ("Automatisch – wer auf Platz {0} ist (empfohlen)", "Automatic – whoever is in slot {0} (recommended)"),
        ("Fest: {0}", "Fixed: {0}"),
        (" (jetzt Spieler {0})", " (currently player {0})"),
        ("Modus: {0}", "Mode: {0}"),
        ("Im Spiel: {0}", "In game: {0}"),
        ("Spiele ({0}, davon {1} mit eigenem Profil ●)", "Games ({0}, {1} with own profile ●)"),
        ("PadForge {0} installiert", "PadForge {0} installed"),
        ("{0} Controller verbunden", "{0} controller(s) connected"),
        ("Tastatur = Spieler {0}", "Keyboard = player {0}"),
        ("Akku {0} %", "Battery {0} %"),
        ("Akku {0} % (lädt)", "Battery {0} % (charging)"),
        ("Sensortest · {0}", "Sensor test · {0}"),
        (" · DolphinBar-Slot {0}", " · DolphinBar slot {0}"),
        ("Batterie ca. {0} %", "Battery approx. {0} %"),
        ("Batterie ca. {0} % – bald wechseln!", "Battery approx. {0} % – replace soon!"),
        ("Update verfügbar: {0}", "Update available: {0}"),
        ("{0} von {1} Komponenten installiert", "{0} of {1} components installed"),
        (" · {0} Update(s)", " · {0} update(s)"),
        ("Version {0}", "Version {0}"),
        ("Lokales Paket gefunden: {0}", "Local package found: {0}"),
        ("{0} installieren", "Install {0}"),
        ("{0} installiert ✓", "{0} installed ✓"),
        ("   ·   installiert: {0}", "   ·   installed: {0}"),
        ("   ·   aktuell: {0}", "   ·   latest: {0}"),
        ("Spielzeit: {0}", "Playtime: {0}"),
        ("Statistik ({0})", "Statistics ({0})"),
        ("{0} Sessions   ·   {1} Starts", "{0} sessions   ·   {1} starts"),
        (" ({0} fehlgeschlagen)", " ({0} failed)"),
        ("   ·   Ø {0}   ·   längste {1}", "   ·   Ø {0}   ·   longest {1}"),
        ("   ·   letzter Start {0}", "   ·   last start {0}"),
        ("   ·   zuletzt {0}", "   ·   last {0}"),
        ("{0} nicht installiert", "{0} not installed"),
        ("{0} nicht gefunden", "{0} not found"),
        ("{0} fehlt (Komponenten)", "{0} missing (Components)"),
        ("Datei: {0}", "File: {0}"),
        ("Das Bild konnte nicht übernommen werden: {0}", "The image could not be applied: {0}"),
        ("Preset: {0}", "Preset: {0}"),
        ("★ {0} ist jetzt Favorit", "★ {0} is now a favorite"),
        ("{0} aus Favoriten entfernt", "{0} removed from favorites"),
        ("{0} Spiele", "{0} games"),
        ("Laufzeit: {0} · Exit-Code: {1}", "Runtime: {0} · exit code: {1}"),
        ("{0}   ·   Spielzeit: {1}   ·   {2}", "{0}   ·   Playtime: {1}   ·   {2}"),
        ("Spielzeit: {0}   ·   {1}", "Playtime: {0}   ·   {1}"),
        ("Meine Miis ({0})", "My Miis ({0})"),
        ("   ·   erstellt von {0}", "   ·   created by {0}"),
        ("Quelle: {0}", "Source: {0}"),
        ("   ·   Profil: {0}", "   ·   Profile: {0}"),
        ("„{0}“ aus dem Hub löschen? In den Konsolen bleibt es erhalten.", "Delete “{0}” from the hub? It stays on the consoles."),
        ("„{0}“ zuweisen", "Assign “{0}”"),
        ("„{0}“ ist jetzt das Mii von Profil „{1}“.", "“{0}” is now the Mii of profile “{1}”."),
        ("{0} Mii(s) aus {1} übernommen ✓", "{0} Mii(s) imported from {1} ✓"),
        ("Keine neuen Miis in {0} gefunden", "No new Miis found in {0}"),
        ("Mii-Import aus {0} fehlgeschlagen", "Mii import from {0} failed"),
        ("„{0}“ importiert ✓", "“{0}” imported ✓"),
        ("Mod „{0}“ importiert ✓", "Mod “{0}” imported ✓"),
        ("{0} wird aktiviert …", "Enabling {0} …"),
        ("{0} wird deaktiviert …", "Disabling {0} …"),
        ("Retro Rewind {0} bereit", "Retro Rewind {0} ready"),
        ("WiiCompiled {0} bereit", "WiiCompiled {0} ready"),
        ("Dolphin {0} bereit (Fallback)", "Dolphin {0} ready (fallback)"),
        ("CTGP Deluxe {0} bereit", "CTGP Deluxe {0} ready"),
        ("Retro Rewind {0} installiert", "Retro Rewind {0} installed"),
        ("WiiCompiled {0} installiert", "WiiCompiled {0} installed"),
        ("Dolphin {0} installiert", "Dolphin {0} installed"),
        ("Bereit: {0} mit {1}", "Ready: {0} with {1}"),
        ("Gesamte Spielzeit: {0}", "Total playtime: {0}"),
        ("{0} Spiele in der Bibliothek · {1} Favoriten", "{0} games in library · {1} favorites"),
        ("Profil „{0}“ aktiv", "Profile “{0}” active"),
        ("Profil „{0}“ löschen? Spielstand-Sicherungen und Statistik bleiben erhalten.", "Delete profile “{0}”? Save backups and statistics are kept."),
        ("Profil: {0}", "Profile: {0}"),
        ("   ·   {0} Sicherung(en)", "   ·   {0} backup(s)"),
        ("Sicherungen von {0} ({1})", "Backups of {0} ({1})"),
        ("Für „{0}“ kopiert ✓", "Copied for “{0}” ✓"),
        ("{0} Spiele · {1} Dateien geprüft · ", "{0} games · {1} files checked · "),
        ("{0} DLC(s), {1} Update(s) erkannt", "{0} DLC(s), {1} update(s) detected"),
        ("Schritt {0} von {1}: {2}", "Step {0} of {1}: {2}"),
        ("{0} als {1} übernehmen", "Use {0} as {1}"),
        ("{0} {1} gefunden", "{0} {1} found"),
        ("{0} Spiele importiert ({1} Dateien geprüft).\n", "{0} games imported ({1} files checked).\n"),
        ("{0} Min.", "{0} min"),
        ("{0} Std. {1} Min.", "{0} h {1} min"),
        ("Vor {0} Tagen gespielt", "Played {0} days ago"),
        ("Spielstand-Ordner für {0} nicht ermittelbar", "Save folder for {0} could not be determined"),
        ("Kein Emulator für {0} verfügbar.", "No emulator available for {0}."),
        ("Update: {0}", "Update: {0}"),
        ("DLC-Dateien: {0} · Update: {1}", "DLC files: {0} · update: {1}"),
        ("{0} DLC-Datei(en) gefunden, {1} im Emulator eingetragen", "{0} DLC file(s) found, {1} registered in the emulator"),
        ("{0} Datei(en) im Switch-Emulator eingetragen.", "{0} file(s) registered in the Switch emulator."),
        ("Retro Rewind {0} – Update {1} verfügbar", "Retro Rewind {0} – update {1} available"),
        ("Retro Rewind {0} – aktuell", "Retro Rewind {0} – up to date"),
        ("Zuletzt {0}", "Last played {0}"),
    ];
}
