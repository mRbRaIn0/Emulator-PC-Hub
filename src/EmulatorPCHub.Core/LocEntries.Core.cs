namespace EmulatorPCHub.Core;

/// <summary>Deutsch → Englisch (Teil 1: allgemeine Begriffe, Navigation, Einstellungen, Spielseiten).</summary>
internal static partial class Entries
{
    public static readonly Dictionary<string, string> Plain = Build();

    private static Dictionary<string, string> Build()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void A(string de, string en) => d[de] = en;

        // ── Allgemein ──────────────────────────────────────────────
        A("Speichern", "Save"); A("Abbrechen", "Cancel"); A("Zurück", "Back"); A("Weiter", "Next");
        A("Fertig", "Done"); A("Öffnen", "Open"); A("Löschen", "Delete"); A("Entfernen", "Remove");
        A("Starten", "Start"); A("Spielen", "Play"); A("Name", "Name"); A("Status", "Status");
        A("Zuletzt", "Last played"); A("Spielzeit", "Playtime"); A("Startet mit", "Launches with");
        A("Kein", "None"); A("Keine", "None"); A("Standard", "Default"); A("Aus", "Off"); A("An", "On");
        A("Ja", "Yes"); A("Nein", "No"); A("Anlegen", "Create"); A("Installieren", "Install");
        A("Umbenennen", "Rename"); A("Wiederherstellen", "Restore"); A("Duplizieren", "Duplicate");
        A("Ordner öffnen", "Open folder"); A("Ordner wählen", "Choose folder"); A("Ordner hinzufügen", "Add folder");
        A("Erneut suchen", "Search again"); A("Neu suchen", "Search again"); A("Aktivieren", "Activate");
        A("Überspringen", "Skip"); A("Übernehmen", "Apply"); A("Optionen", "Options"); A("Tastatur", "Keyboard");
        A("Automatisch", "Automatic"); A("Gespeichert", "Saved"); A("Fehler", "Error"); A("Unbekannt", "Unknown");
        A("Unbekannter Fehler", "Unknown error"); A("Verbunden", "Connected"); A("Nicht gefunden", "Not found");
        A("Nicht installiert", "Not installed"); A("Kein Controller", "No controller");
        A("Offizielle Webseite", "Official website"); A("Changelog", "Changelog");
        A("Ⓐ Auswählen   Ⓑ Zurück", "Ⓐ Select   Ⓑ Back");
        A("Ⓑ Zurück", "Ⓑ Back");
        A("Ⓐ Ändern   Ⓑ Zurück", "Ⓐ Change   Ⓑ Back");
        A("Ⓐ Umschalten   Ⓑ Zurück", "Ⓐ Toggle   Ⓑ Back");
        A("Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Nach Updates suchen", "Ⓐ Select   Ⓑ Back   Ⓧ Check for updates");
        A("Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Favorit   Ⓨ Starten", "Ⓐ Select   Ⓑ Back   Ⓧ Favorite   Ⓨ Start");
        A("Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Vibration testen   Ⓨ Beitreten", "Ⓐ Select   Ⓑ Back   Ⓧ Test rumble   Ⓨ Join");
        A("Ⓐ Auswählen   Ⓑ Zurück   Ⓨ Spielen", "Ⓐ Select   Ⓑ Back   Ⓨ Play");
        A("Ⓐ Auswählen   Ⓑ Zurück   Ⓧ Retro Rewind prüfen   Ⓨ Spielen", "Ⓐ Select   Ⓑ Back   Ⓧ Check Retro Rewind   Ⓨ Play");
        A("Ⓐ Auswählen   Ⓑ Zurück   L/R Bereich wechseln", "Ⓐ Select   Ⓑ Back   L/R Switch section");
        A("Ⓐ Auswählen   Ⓑ Zurück   R Weiter   L Zurück", "Ⓐ Select   Ⓑ Back   R Next   L Back");
        A("Ⓐ Auswählen   Ⓑ Zurück   L/R Profil wechseln", "Ⓐ Select   Ⓑ Back   L/R Switch profile");
        A("Ⓐ Starten   Ⓧ Favorit   Ⓨ Optionen   ⊕ Einstellungen", "Ⓐ Start   Ⓧ Favorite   Ⓨ Options   ⊕ Settings");
        A("Ⓐ Spiel starten / Einstellungen   Ⓑ Zurück   L/R Filter wechseln", "Ⓐ Start game / Settings   Ⓑ Back   L/R Switch filter");
        A("Spiel beenden: Home + Minus (Guide + View) 1,5 s halten", "Quit game: hold Home + Minus (Guide + View) for 1.5 s");

        // ── Titel der Seiten ───────────────────────────────────────
        A("Einstellungen", "Settings"); A("Statistik", "Statistics"); A("Spielstände", "Saves");
        A("Komponenten", "Components"); A("Profile", "Profiles"); A("Alle Spiele", "All games");
        A("Controller", "Controllers"); A("Tipps", "Tips"); A("Deine Statistik", "Your statistics");
        A("Spiel nicht gefunden", "Game not found"); A("Logs", "Logs");
        A("Mii Manager", "Mii Manager"); A("Mods", "Mods");
        A("Spielzeit nach Plattform", "Playtime by platform"); A("Spielzeit nach Profil", "Playtime by profile");
        A("Spielzeit nach Preset / Mod", "Playtime by preset / mod"); A("Spiele", "Games");

        // ── Einstellungen ──────────────────────────────────────────
        A("Allgemein", "General"); A("Bibliothek", "Library"); A("Emulatoren", "Emulators"); A("Spielstart", "Launch");
        A("Profil", "Profile"); A("Über", "About"); A("Troubleshooting", "Troubleshooting");
        A("Sprache", "Language"); A("Erstsprache", "Primary language"); A("Zweitsprache", "Secondary language");
        A("Beim Start im Console Mode (randloses Vollbild)", "Start in Console Mode (borderless fullscreen)");
        A("Helles Design (Basic White)", "Light theme (Basic White)");
        A("UI-Sounds", "UI sounds"); A("Animationen", "Animations"); A("24-Stunden-Uhr", "24-hour clock");
        A("Mario-Kart-Kacheln auch ohne Dump anzeigen", "Show Mario Kart tiles even without a dump");
        A("Mit Windows starten (direkt im Console Mode)", "Start with Windows (straight into Console Mode)");
        A("Lautstärke der UI-Sounds", "UI sound volume"); A("Leise", "Quiet"); A("Mittel", "Medium"); A("Laut", "Loud");
        A("Einrichtungs-Assistent erneut starten", "Run setup assistant again");
        A("Spiele müssen nicht in den Hub-Ordner kopiert werden – bestehende Speicherorte werden eingebunden. ", "Games do not have to be copied into the hub folder – existing locations are used as they are. ");
        A("Eigene Dumps werden anhand des Disc-Headers bzw. der Title-ID erkannt, Updates und DLCs automatisch zugeordnet.", "Your own dumps are recognized by their disc header or title ID; updates and DLC are matched automatically.");
        A("Kein Ordner gewählt", "No folder selected"); A("Weitere Ordner (Plattform automatisch)", "More folders (platform detected automatically)");
        A("Bibliothek jetzt scannen", "Scan library now"); A("Scanne …", "Scanning …");
        A("Daten: ", "Data: "); A("EXE manuell wählen", "Choose EXE manually"); A("Automatisch erkennen", "Detect automatically");
        A("Der Switch-Emulator ist als Adapter austauschbar. Registrierte Adapter: ", "The Switch emulator can be swapped as an adapter. Registered adapters: ");
        A(". Aktiv: ", ". Active: ");
        A("Switch-Emulator", "Switch emulator");
        A("Konfiguration: ", "Configuration: "); A("Mit Hub-Dolphin verbinden", "Connect to hub Dolphin");
        A("Wheel Wizard nutzt jetzt Dolphin und Spiel des Hubs ✓", "Wheel Wizard now uses the hub's Dolphin and game ✓");
        A("Wheel Wizard öffnen", "Open Wheel Wizard");
        A("Hub während des Spiels ausblenden (sonst minimieren)", "Hide hub while playing (otherwise minimize)");
        A("Spiele im Vollbild starten", "Start games in fullscreen");
        A("Home + Minus (1,5 s halten) beendet das laufende Spiel", "Home + Minus (hold 1.5 s) quits the running game");
        A("Vor dem Start Konfiguration sichern", "Back up configuration before launch");
        A("Ablauf: Preset laden → Dateien prüfen → Controllerprofil → Mods aktivieren → Emulator starten → Hub ausblenden → ", "Flow: load preset → check files → controller profile → enable mods → start emulator → hide hub → ");
        A("Spiel läuft → Spielzeit speichern → Hub wieder anzeigen.", "game runs → save playtime → show hub again.");
        A("Hintergrund", "Background"); A("Haut", "Skin"); A("Haarfarbe", "Hair color"); A("Frisur", "Hairstyle");
        A("Augen", "Eyes"); A("Mund", "Mouth"); A("Shirt", "Shirt"); A("Profil hinzufügen", "Add profile");
        A("Neues Profil", "New profile");
        A("Pro Spielstart werden Spiel, Preset, Emulator, Argumente, Fehler, Exit-Code und Laufzeit protokolliert.", "Every launch logs the game, preset, emulator, arguments, errors, exit code and runtime.");
        A("Log-Ordner öffnen", "Open log folder"); A("View Logs", "View logs"); A("Restore Backup", "Restore backup");
        A("Backups", "Backups"); A("Backup-Ordner öffnen", "Open backup folder"); A("Ordner", "Folders");
        A("Hub-Daten (data)", "Hub data (data)"); A("Komponenten (integrations)", "Components (integrations)");
        A("Eigene Oberfläche für GameCube, Wii, Wii U und Switch auf dem PC – mit Dolphin, Cemu, Eden, WiiCompiled, ", "A dedicated interface for GameCube, Wii, Wii U and Switch on PC – with Dolphin, Cemu, Eden, WiiCompiled, ");
        A("Wheel Wizard, Retro Rewind und CTGP Deluxe.", "Wheel Wizard, Retro Rewind and CTGP Deluxe.");
        A("Stammordner: ", "Root folder: ");
        A("Dies ist kein offizielles Nintendo-Produkt. Es werden keine Spiele, Firmware, Keys oder Nintendo-Assets ", "This is not an official Nintendo product. No games, firmware, keys or Nintendo assets are ");
        A("mitgeliefert oder heruntergeladen – nur deine eigenen Dumps werden verwendet.", "bundled or downloaded – only your own dumps are used.");

        // ── Startseite / Navigation ────────────────────────────────
        A("Controller-Empfehlung", "Controller recommendation"); A("Öffnen", "Open"); A("Statistik", "Statistics");
        A("Eigenen Dump hinzufügen", "Add your own dump"); A("Einrichtung nötig", "Setup required");
        A("Tipp: Unter Einstellungen → Bibliothek deine Spieleordner eintragen – eigene Dumps werden automatisch erkannt.", "Tip: add your game folders under Settings → Library – your own dumps are recognized automatically.");

        // ── Bibliothek / Spielseite / Spieleinstellungen ───────────
        A("Alle", "All"); A("★ Favoriten", "★ Favorites"); A("Zuletzt gespielt", "Recently played");
        A("   ·   im Hauptmenü ausgeblendet", "   ·   hidden on home screen"); A("Spiel starten", "Start game");
        A("★ Favorit", "★ Favorite"); A("☆ Als Favorit", "☆ Add to favorites");
        A("Preset", "Preset"); A("Eigenes Preset (Startparameter) …", "Custom preset (launch parameters) …");
        A("Gesamtstatistik", "Overall statistics"); A("Emulator", "Emulator"); A("Cover wählen", "Choose cover");
        A("Controller-Profil", "Controller profile"); A("Eigenes Preset", "Custom preset");
        A("Zusätzliche Startparameter", "Additional launch parameters"); A("Preset anlegen", "Create preset");
        A("z. B. -C Dolphin.Core.GFXBackend=Vulkan", "e.g. -C Dolphin.Core.GFXBackend=Vulkan");
        A("Name im Hauptmenü", "Name in main menu"); A("Name speichern", "Save name");
        A("Originalnamen verwenden", "Use original name"); A("Cover", "Cover"); A("Kein Cover", "No cover");
        A("Standard-Kachel (kein Bild)", "Default tile (no image)"); A("Bild wählen …", "Choose image …");
        A("Ausschnitt anpassen …", "Adjust crop …"); A("Bild im Explorer zeigen", "Show image in Explorer");
        A("Cover entfernen", "Remove cover"); A("Cover entfernt", "Cover removed"); A("Spieldatei", "Game file");
        A("Keine Datei (eingebautes Spiel)", "No file (built-in game)"); A("Dateipfad öffnen", "Open file path");
        A("Hauptmenü", "Main menu"); A("Im Hauptmenü ausblenden", "Hide on home screen");
        A("Ausgeblendete Spiele bleiben unter „Alle Spiele“ sichtbar und startbar.", "Hidden games stay visible and playable under “All games”.");
        A("Name gespeichert ✓", "Name saved ✓"); A("Cover gespeichert ✓", "Cover saved ✓");
        A("Cover konnte nicht übernommen werden", "Cover could not be applied");
        A("Titel & Cover pro Sprache", "Title & cover per language");
        A("Sprache dieses Eintrags", "Language of this entry");
        A("Titel und Cover lassen sich pro Sprache festlegen. Angezeigt wird die Variante der Erstsprache, sonst der Zweitsprache, sonst der Standard.", "Title and cover can be set per language. The primary language variant is shown, otherwise the secondary language, otherwise the default.");
        A("Cover zuschneiden", "Crop cover"); A("Ausschnitt verschieben", "Move crop"); A("Zoom", "Zoom");
        A("Maus: ziehen zum Verschieben, Mausrad zum Zoomen.", "Mouse: drag to move, mouse wheel to zoom.");
        A("Noch nicht gespielt", "Not played yet"); A("Weniger als 1 Minute", "Less than 1 minute");
        A("Heute gespielt", "Played today"); A("Gestern gespielt", "Played yesterday");

        // ── Statistik ──────────────────────────────────────────────
        A("Alle Profile", "All profiles"); A("ohne Profil (älter)", "no profile (older)"); A("gelöschtes Profil", "deleted profile");
        A("Noch keine Spielzeit erfasst. Starte ein Spiel über den Hub – danach erscheint hier die Auswertung.", "No playtime recorded yet. Start a game from the hub – the analysis will appear here.");
        A("Gesamtspielzeit", "Total playtime"); A("Sessions", "Sessions"); A("Starts", "Starts");
        A("Ø Session", "Ø session"); A("Längste Session", "Longest session"); A("Letzter Start", "Last start");
        A("Meistgespielte Woche", "Most played week"); A("Meistgespielter Monat", "Most played month");
        A("Verlauf", "History"); A("Letzte 12 Wochen  ⇄  30 Tage", "Last 12 weeks  ⇄  30 days");
        A("Letzte 30 Tage  ⇄  12 Wochen", "Last 30 days  ⇄  12 weeks");

        // ── Spielstände ────────────────────────────────────────────
        A("Spielstände von …", "Saves of …"); A("Automatische Snapshots vor/nach dem Spielen", "Automatic snapshots before/after playing");
        A("Spielstände pro Spiel und Profil sichern und wiederherstellen. Profile selbst verwaltest du unter „Profile“.", "Back up and restore saves per game and profile. Manage the profiles themselves under “Profiles”.");
        A("Spielstand vorhanden", "Save exists"); A("Noch kein Spielstand", "No save yet"); A("Spielstände – ", "Saves – ");
        A("Speicherorte", "Save locations");
        A("Für dieses Spiel ist kein Speicherort bekannt (Emulator einmal starten).", "No save location is known for this game (start the emulator once).");
        A("Jetzt sichern", "Back up now"); A("Sicherung importieren …", "Import backup …");
        A("Noch keine Sicherungen. Automatische Snapshots entstehen vor und nach dem Spielen, sobald ein Spielstand existiert.", "No backups yet. Automatic snapshots are created before and after playing as soon as a save exists.");
        A("   ·   automatisch", "   ·   automatic"); A("Für Profil duplizieren …", "Duplicate for profile …");
        A("Sicherung löschen", "Delete backup"); A("Diese Sicherung endgültig löschen?", "Delete this backup permanently?");
        A("Spielstand sichern", "Back up save"); A("Bezeichnung (optional)", "Label (optional)");
        A("Manuelle Sicherung", "Manual backup"); A("Spielstand gesichert ✓", "Save backed up ✓");
        A("Kein Spielstand vorhanden", "No save available"); A("Spielstand wiederherstellen", "Restore save");
        A("Der aktuelle Stand wird vorher automatisch gesichert.", "The current state is backed up automatically first.");
        A("Spielstand wiederhergestellt ✓", "Save restored ✓"); A("Save Manager", "Save Manager");
        A("Es gibt nur ein Profil. Lege unter „Profile“ ein weiteres an (z. B. Spieler 2).", "There is only one profile. Add another under “Profiles” (e.g. Player 2).");
        A("Sicherung kopieren für …", "Copy backup for …");
        A("Die Sicherung erscheint danach beim anderen Profil und kann dort wiederhergestellt werden.", "The backup will then appear for the other profile and can be restored there.");
        A("Sicherung importiert ✓ – jetzt „Wiederherstellen“ wählen", "Backup imported ✓ – now choose “Restore”");
        A("Sicherung", "Backup");

        // ── Profile ────────────────────────────────────────────────
        A("Jedes Profil hat eigene Favoriten, Statistik und – über Cemu-Konto bzw. Eden-Benutzer – eigene Spielstände.", "Every profile has its own favorites, statistics and – via Cemu account or Eden user – its own saves.");
        A("   ✓ aktiv", "   ✓ active"); A("Mii: ", "Mii: "); A("keins (eigener Avatar)", "none (own avatar)");
        A("Controller: ", "Controller: "); A("festgelegt (gerade nicht verbunden)", "fixed (not connected right now)");
        A(" (Standard)", " (default)"); A("Profil umbenennen", "Rename profile"); A("Mii wählen", "Choose Mii");
        A("Avatar bearbeiten", "Edit avatar"); A("Cemu-Konto", "Cemu account"); A("Eden-Benutzer", "Eden user");
        A("Name (z. B. Gast oder Spieler 2)", "Name (e.g. Guest or Player 2)");
        A("Kein Mii (eigenen Avatar verwenden)", "No Mii (use own avatar)"); A("Mii für ", "Mii for ");
        A("Noch keine Miis – im Mii Manager importieren oder erstellen.", "No Miis yet – import or create them in the Mii Manager.");
        A("Automatisch (keine Festlegung)", "Automatic (no fixed choice)"); A("Controller für ", "Controller for ");
        A("Der gewählte Controller wird beim Wechsel auf dieses Profil automatisch Spieler 1.", "The chosen controller automatically becomes player 1 when switching to this profile.");
        A("Cemu hat noch keine Konten – Cemu einmal starten. Weitere Konten legst du in Cemu unter ", "Cemu has no accounts yet – start Cemu once. Create more accounts in Cemu under ");
        A("Optionen → Allgemeine Einstellungen → Konto an.", "Options → General settings → Account.");
        A("Wii-U-Konto für ", "Wii U account for ");
        A("Beim Start eines Wii-U-Spiels aktiviert der Hub dieses Cemu-Konto – so hat jedes Profil eigene Spielstände.", "When a Wii U game starts, the hub activates this Cemu account – so every profile has its own saves.");
        A("Eden hat noch keine Benutzer – Eden einmal starten. Weitere Benutzer legst du in Eden unter ", "Eden has no users yet – start Eden once. Create more users in Eden under ");
        A("Emulation → Konfigurieren → System → Profile an.", "Emulation → Configure → System → Profiles.");
        A("Switch-Benutzer für ", "Switch user for ");
        A("Beim Start eines Switch-Spiels aktiviert der Hub diesen Eden-Benutzer – so hat jedes Profil eigene Spielstände.", "When a Switch game starts, the hub activates this Eden user – so every profile has its own saves.");
        A("Profil löschen", "Delete profile");

        // ── News / Home-Kacheln ────────────────────────────────────
        A("Mario Kart – bereit zum Spielen?", "Mario Kart – ready to play?");
        A("Eigener Dump fehlt noch (ISO/WBFS/RVZ)", "Your own dump is still missing (ISO/WBFS/RVZ)"); A("Dump: ", "Dump: ");
        A("Retro Rewind fehlt", "Retro Rewind missing"); A("WiiCompiled noch nicht gebaut (braucht PAL-Dump)", "WiiCompiled not built yet (needs PAL dump)");
        A("Dolphin fehlt", "Dolphin missing"); A("Zu Mario Kart Wii", "Go to Mario Kart Wii");
        A("Eigener Dump fehlt noch (NSP/XCI)", "Your own dump is still missing (NSP/XCI)"); A("Keys vorhanden", "Keys present");
        A("Eigene Keys (prod.keys) fehlen", "Your own keys (prod.keys) are missing"); A("Firmware installiert", "Firmware installed");
        A("Eigene Firmware fehlt", "Your own firmware is missing"); A("CTGP Deluxe fehlt", "CTGP Deluxe missing");
        A("Zu Mario Kart 8 Deluxe", "Go to Mario Kart 8 Deluxe");
        A("• F11 oder das Vollbild-Symbol oben rechts schaltet den Console Mode um.", "• F11 or the fullscreen icon at the top right toggles Console Mode.");
        A("• Home + Minus 1,5 s halten beendet ein laufendes Spiel und bringt dich zurück in den Hub.", "• Holding Home + Minus for 1.5 s quits a running game and takes you back to the hub.");
        A("• Unter Komponenten siehst du, ob es neue Versionen von Dolphin, Cemu, WiiCompiled, Retro Rewind & Co. gibt.", "• Under Components you can see whether there are new versions of Dolphin, Cemu, WiiCompiled, Retro Rewind & co.");

        // ── Power-Menü ─────────────────────────────────────────────
        A("Energie sparen (Sleep)", "Sleep"); A("Neu starten", "Restart"); A("Herunterfahren", "Shut down");
        A("Windows-Desktop sperren", "Lock Windows desktop"); A("Zu Windows zurückkehren (Hub beenden)", "Return to Windows (quit hub)");
        A("Den PC jetzt neu starten?", "Restart the PC now?"); A("Den PC jetzt herunterfahren?", "Shut down the PC now?");

        // ── Logs ───────────────────────────────────────────────────
        A("Spielstarts", "Game launches"); A("Noch keine Spielstarts.", "No game launches yet.");
        A("Hub-Log (aktuelle Sitzung)", "Hub log (current session)");

        // ── Komponenten ────────────────────────────────────────────
        A("   ·   Suche nach Updates …", "   ·   Checking for updates …"); A("Nach Updates suchen", "Check for updates");
        A("integrations-Ordner öffnen", "Open integrations folder");
        A("Der Hub lädt nichts herunter. Lade Emulatoren, Tools und Mods selbst von der offiziellen ", "The hub downloads nothing. Download emulators, tools and mods yourself from the official ");
        A("Webseite des jeweiligen Projekts und installiere sie hier „Aus Datei“ (oder lege das Archiv in ", "website of the respective project and install them here via “From file” (or put the archive in ");
        A("integrations\\_downloads ab). Spiele, Keys und Firmware stellst du aus deinen eigenen Dumps bereit.", "integrations\\_downloads). Games, keys and firmware come from your own dumps.");
        A("kein Windows-Paket (z. B. Quellcode oder Webseite). Die Datei kann gelöscht werden.", "not a Windows package (e.g. source code or web page). The file can be deleted.");
        A("Vorhandene Einstellungen bleiben erhalten (vorher Backup).", "Existing settings are kept (backed up first).");
        A("Aus Datei installieren …", "Install from file …"); A("Installiert ✓", "Installed ✓"); A("Installiert – prüfen", "Installed – check");
        A("Update aus Datei …", "Update from file …"); A("Reparieren aus Datei …", "Repair from file …"); A("Fertig ✓", "Done ✓");

        AddControllers(A);
        AddGamePages(A);
        return d;
    }
}
