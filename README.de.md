# Emulator PC Hub

🇩🇪 Deutsch · [🇬🇧 English](README.md)

Eine controllerfreundliche Windows-Oberfläche (C# / .NET 10 / WinUI 3), die deine **selbst installierten Emulatoren**
und deine **eigenen Spiele-Dumps** in einer Konsolen-Oberfläche zusammenführt:

> PC starten → Emulator PC Hub → Spiel auswählen → spielen → nach dem Beenden automatisch zurück im Hub.

Der Hub ist nur die **Hülle bzw. der Launcher**. Er enthält keine Spiele, ROMs, BIOS-Dateien, Firmware, Keys, Emulatoren
oder Mods und **lädt so etwas auch nicht herunter**. Alles, was gespielt wird, stellst du selbst bereit (siehe
[Was du selbst bereitstellen musst](#was-du-selbst-bereitstellen-musst)).

---

## Inhalt

- [Funktionen](#funktionen)
- [Unterstützte Komponenten](#unterstützte-komponenten)
- [Voraussetzungen](#voraussetzungen)
- [Was du selbst bereitstellen musst](#was-du-selbst-bereitstellen-musst)
- [Installation](#installation)
- [Einrichtung](#einrichtung)
- [Bedienung](#bedienung)
- [Ordnerstruktur](#ordnerstruktur)
- [Netzwerk & Datenschutz](#netzwerk--datenschutz)
- [Aus dem Quellcode bauen](#aus-dem-quellcode-bauen)
- [Rechtliches](#rechtliches)

---

## Funktionen

**Oberfläche & Start**
- Console Mode (randloses Vollbild) und Desktop-Fenster, Umschalten mit F11
- Komplett mit Controller bedienbar, eigene UI-Sounds (vom Hub selbst erzeugt, keine fremden Audiodateien)
- Spiel starten, Hub ausblenden, auf das Spielende warten (inkl. Kindprozesse), Hub wieder in den Vordergrund holen
- Laufendes Spiel per Tastenkombination beenden (Home + Minus 1,5 s halten)
- Optional: mit Windows starten, Verknüpfungen, direkter Spielstart per `--launch <Spiel-ID>`
- Einrichtungsassistent beim ersten Start
- Oberfläche auf Deutsch und Englisch; Erst- und Zweitsprache in den Einstellungen wählbar

**Bibliothek**
- Scannt deine Spieleordner und erkennt Titel anhand von Disc-Headern bzw. Title-IDs
- Erkennt Updates und DLCs zu einem Spiel und bindet sie im Emulator als externe Inhalte ein (keine NAND-Installation nötig)
- Favoriten, zuletzt gespielt, Filter pro System, eigene Cover (Zuschneiden im Hub) – ohne Cover erzeugt der Hub
  eine eigene Kachel aus Systemfarbe, Monogramm und Titel
- Titel und Cover pro Sprache (Wii U: Titel aus den Spieldaten; sonst eigene Einträge), angezeigt wird die Erst-, sonst die Zweitsprache
- Pro Spiel eigene Einstellungen und Presets (z. B. „Vanilla“ oder eine Mod-Edition)

**Controller**
- Automatische Erkennung über SDL3 (Xbox, DualSense/DualShock, Pro Controller, Joy-Con, Tastatur u. a.) inkl. Hotplug,
  Akkustand, Vibration und Spieler 1–4 (tauschbar)
- Controller-Profile pro Spiel, Tasten-Remapping, Bewegungssteuerung bei geeigneten Controllern
- Beim Spielstart schreibt der Hub die Belegung direkt in die Konfiguration des jeweiligen Emulators
  (Dolphin, Cemu, Eden, melonDS, Azahar)
- Optionale Kompatibilitätsschicht über PadForge (virtueller Xbox-Controller), nur wenn ein Emulator einen
  Controller nicht direkt unterstützt
- Unterstützung für Original-Bewegungsfernbedienungen per Bluetooth bzw. DolphinBar

**Mods & Presets**
- Mod-Verwaltung pro Spiel: Mods aktivieren/deaktivieren, der Hub schaltet sie beim Start passend zur gewählten Edition um
- Import eigener Mod-Pakete (ZIP/Ordner), z. B. Retro Rewind oder CTGP-DX
- Integration von Wheel Wizard und WiiCompiled (falls selbst installiert)
- Cemu-Graphic-Packs verwalten

**Profile, Avatare, Spielstände, Statistik**
- Hub-Profile (z. B. „Spieler 1“, „Gast“) mit Avatar, bevorzugtem Controller, eigenen Favoriten und Statistik;
  pro Profil wählbare Emulator-Benutzer (Cemu-Konto, Eden-Benutzer) → getrennte Spielstände
- Avatar-Manager: Avatare aus den Emulator-Datenbanken importieren, exportieren, umbenennen und zuweisen
  (nur mit deinen eigenen Emulator-Daten; der Hub liefert keine Avatar-Grafiken mit)
- Save Manager: Spielstände pro Spiel und Profil sichern, wiederherstellen, exportieren/importieren, duplizieren;
  automatische Snapshots vor/nach jedem Spiel (max. 10 pro Spiel/Profil)
- Statistik: Spielzeit, Sessions, Starts, Ø/längste Session, Verlauf (30 Tage / 12 Wochen), nach System, Profil und Preset

**Komponenten-Verwaltung**
- Zeigt, welche Emulatoren/Tools installiert sind, und prüft Pfade und Konfiguration
- Optional: Abfrage der aktuellen Versionsnummer beim offiziellen Projekt (nur Versionsinfo, **kein Download**)
- Installation und Update ausschließlich aus einer **von dir selbst heruntergeladenen Datei** („Aus Datei installieren …“);
  das Paket wird vorher geprüft, vorhandene Konfiguration wird gesichert
- Portable Installation unter `integrations\` – nichts wird in Systemordner geschrieben

**Sonstiges**
- Automatische Backups (Konfiguration, Presets, Mods, Spielstände), Log-Ansicht, Start-Protokoll (`Logs\launches.jsonl`)
- Portables Layout: alle Daten liegen neben dem Hub (Fallback `%LOCALAPPDATA%\EmulatorPCHub`, wenn der Ordner schreibgeschützt ist)

---

## Unterstützte Komponenten

Alle Komponenten sind **eigenständige Projekte Dritter**. Sie sind nicht im Hub enthalten; du lädst sie selbst von der
offiziellen Seite herunter und installierst sie im Hub unter **Komponenten → „Aus Datei installieren …“**.

| Komponente | Typ | Offizielle Seite | Erwartetes Paket |
|---|---|---|---|
| Dolphin | Emulator | https://dolphin-emu.org | Windows-Archiv (ZIP/7z) mit `Dolphin.exe` |
| Cemu | Emulator | https://cemu.info | Windows-ZIP mit `Cemu.exe` |
| Eden | Emulator | https://eden-emu.dev | Windows-ZIP mit `eden.exe` |
| melonDS | Emulator | https://melonds.kuribo64.net | Windows-ZIP mit `melonDS.exe` |
| Azahar | Emulator | https://azahar-emu.org | Windows-ZIP mit `azahar.exe` |
| WiiCompiled | Engine (optional) | https://github.com/patchzyy/Wiicompiled | `WiiCompiled-Setup.exe` |
| Wheel Wizard | Mod-Tool (optional) | https://github.com/TeamWheelWizard/WheelWizard | `WheelWizard.exe` |
| PadForge | Controller-Schicht (optional) | https://github.com/hifihedgehog/PadForge | Windows-ZIP mit `PadForge.exe` |
| Retro Rewind | Mod (optional) | https://rwfc.net | vollständiges Paket als ZIP |
| CTGP-DX | Mod (optional) | https://www.ctgpdx.com | ZIP oder entpackter Ordner |

Bereits vorhandene Installationen (z. B. unter `%LOCALAPPDATA%\Programs\…` oder mit Standard-Benutzerordner) werden
ebenfalls erkannt. Beachte die Lizenzen und Nutzungsbedingungen des jeweiligen Projekts.

**Unterstützte Dateiformate deiner Dumps**

| Emulator | Formate |
|---|---|
| Dolphin | `.iso` `.gcm` `.gcz` `.rvz` `.ciso` `.wia` `.wbfs` `.wad` `.dol` `.elf` |
| Cemu | `.wux` `.wud` `.wua` `.rpx` |
| Eden | `.nsp` `.xci` `.nca` `.nro` (Updates/DLCs als `.nsp`) |
| melonDS | `.nds` `.srl` `.dsi` |
| Azahar | `.3ds` `.cci` `.cxi` `.3dsx` `.z3ds` `.zcci` `.zcxi` |

---

## Voraussetzungen

- Windows 10 (1809 / Build 17763) oder neuer, 64-Bit (x64)
- Für das Release-Paket: **keine** separate .NET- oder Windows-App-SDK-Installation nötig (self-contained)
- Mindestens ein selbst installierter Emulator (siehe oben)
- Deine eigenen, rechtmäßig erstellten Inhalte (siehe nächster Abschnitt)
- Empfohlen: ein Controller (Xbox, PlayStation, Pro Controller o. ä.) – Tastatur und Maus funktionieren ebenfalls
- Zum Bauen aus dem Quellcode: .NET 10 SDK (siehe [Aus dem Quellcode bauen](#aus-dem-quellcode-bauen))

---

## Was du selbst bereitstellen musst

Der Hub funktioniert erst mit Inhalten, die **du selbst besitzt und selbst erstellt hast**. Nichts davon wird
mitgeliefert, angeboten oder automatisch heruntergeladen:

| Inhalt | Wofür | Hinweis |
|---|---|---|
| **Spiele-Dumps** | alle Emulatoren | Nur Dumps von Spielen, die du selbst besitzt und selbst von deiner Hardware bzw. deinen Datenträgern erstellt hast |
| **Updates / DLCs** | Eden (optional) | Selbst von deiner eigenen Konsole gedumpt; der Hub bindet sie nur ein |
| **Keys** (z. B. `prod.keys`, `keys.txt`) | Eden, ggf. Cemu | Von deiner eigenen Konsole ausgelesen. Eden: nach `integrations\switch\user\keys\` kopieren |
| **Firmware** | Eden | Von deiner eigenen Konsole gedumpt; Installation in Eden über *Tools → Install Firmware* |
| **System-Dumps / BIOS** (optional) | z. B. Systemmenüs oder Avatar-Editoren in Dolphin/Cemu/Eden | Nur eigene Dumps; melonDS läuft auch mit seinen freien Ersatz-BIOS |
| **Emulatoren, Tools, Mods** | siehe [Unterstützte Komponenten](#unterstützte-komponenten) | Selbst von der offiziellen Projektseite herunterladen |
| **Cover-Bilder** (optional) | Bibliothek | Eigene Bilder; ohne Cover erzeugt der Hub eigene Kacheln |

> Das Herunterladen von Spielen, Keys oder Firmware aus dem Internet ist in vielen Ländern nicht erlaubt.
> Dieses Projekt unterstützt keine Piraterie und gibt keine Hinweise, wo solche Dateien zu finden sind.

---

## Installation

1. Unter [Releases](../../releases) das Paket `Emulator-PC-Hub-v1.1-win-x64.zip` herunterladen.
2. In einen beliebigen, **beschreibbaren** Ordner entpacken, z. B. `D:\Emulator PC Hub\` (nicht nach `C:\Program Files`,
   sonst weicht der Hub auf `%LOCALAPPDATA%\EmulatorPCHub` aus).
3. `Emulator PC Hub.exe` starten.

Beim ersten Start legt der Hub neben sich die Ordner `data\`, `integrations\`, `Backups\` und `Logs\` an.
Optional kannst du im Stammordner eine leere Datei `EmulatorPCHub.root` anlegen. Dann liegen die Daten dort, auch wenn
die EXE in einem Unterordner (z. B. `App\`) liegt.

> Die EXE ist nicht signiert. Windows SmartScreen kann beim ersten Start warnen („Weitere Informationen → Trotzdem ausführen“).

---

## Einrichtung

Der Einrichtungsassistent führt beim ersten Start durch diese Schritte. Später findest du alles auch einzeln im Hub.

1. **Spieleordner auswählen:** Ordner mit deinen eigenen Dumps hinzufügen (pro System oder gemischt).
2. **Emulatoren installieren:** Im Hub **Komponenten** öffnen → bei einem Emulator **„Offizielle Webseite“** →
   die Windows-Version selbst herunterladen → **„Aus Datei installieren …“** und die heruntergeladene Datei wählen.
   Alternativ das Archiv in `integrations\_downloads\` ablegen; der Hub bietet es dann direkt zur Installation an.
3. **Eigene Keys und Firmware** (nur Eden): `prod.keys` nach `integrations\switch\user\keys\` kopieren, Firmware in Eden
   über *Tools → Install Firmware* installieren.
4. **Controller:** Controller anschließen; der Hub erkennt ihn automatisch. Unter *Controller* kannst du Spieler
   tauschen und Profile anlegen.
5. **Spiele importieren:** Der Hub scannt die Ordner und legt die Bibliothek an. Updates/DLCs werden automatisch zugeordnet.
6. **Optional:** Mods (Mod-Seite bzw. Spielseite → „Aus Datei/Ordner importieren“), Cover, Autostart im Console Mode.

Danach: Spiel auswählen → **Spielen**.

---

## Bedienung

| Taste | Aktion |
|---|---|
| D-Pad / Stick / Pfeiltasten | Navigation |
| A / Enter | Bestätigen |
| B / Esc | Zurück |
| X / Y | Schnellaktionen (z. B. Favorit, Optionen) |
| Start / + | Einstellungen |
| Home / Guide | Zum Homescreen |
| Home + Minus (1,5 s halten) | Laufendes Spiel beenden → zurück zum Hub |
| F11 | Console Mode ein/aus |

**Startparameter**

| Parameter | Wirkung |
|---|---|
| `--console` | Start im Console Mode (Vollbild) |
| `--desktop` | Start im Fenster |
| `--launch <Spiel-ID>` | Spiel direkt starten (z. B. für Verknüpfungen) |

Umgebungsvariable `EPCHUB_ROOT=<Pfad>` legt den Datenordner fest.

---

## Ordnerstruktur

```
Emulator PC Hub/
├── Emulator PC Hub.exe      Hub (Release-Paket)
├── data/                    library.db, config.json, Cover, Profile, Presets, Cache
├── integrations/            deine selbst installierten Emulatoren/Tools/Mods (portabel)
│   └── _downloads/          hier abgelegte Pakete bietet der Hub zur Installation an
├── Backups/                 automatische Backups (Spielstände, Mods, Konfiguration, Presets)
└── Logs/                    Hub-Log und launches.jsonl
```

Quellcode (dieses Repository):

```
├── src/
│   ├── EmulatorPCHub.App/          WinUI-3-Oberfläche (Fenster, Seiten, Controller-Navigation)
│   ├── EmulatorPCHub.UI/           Dienste, ViewModels, Profile, Save Manager
│   ├── EmulatorPCHub.Core/         Modelle, Konfiguration, Pfade, Logs, Backups, Audio
│   ├── EmulatorPCHub.Library/      SQLite-Bibliothek, Scanner, Dateiformat-Analyse, Statistik
│   ├── EmulatorPCHub.Emulation/    Emulator-Adapter, Controller-Konfiguration, Start-Pipeline
│   ├── EmulatorPCHub.Mods/         Presets, Mod-Manager, Graphic Packs
│   ├── EmulatorPCHub.Controllers/  SDL3/XInput, Bluetooth-Fernbedienungen
│   └── EmulatorPCHub.Updates/      Paketprüfung/-installation aus lokalen Dateien, Versionsinfos
├── tests/EmulatorPCHub.Tests/      xUnit-Tests
├── assets/icons/                   eigenes App-Icon
└── build.ps1                       Tests + Release-Build
```

---

## Netzwerk & Datenschutz

- Der Hub lädt **keine Dateien** herunter: keine Spiele, Keys, Firmware, Emulatoren, Tools oder Mods.
- Auf der Seite *Komponenten* fragt der Hub nur die **aktuelle Versionsnummer und den Changelog** bei den offiziellen
  Release-Feeds der Projekte ab (GitHub-API, dolphin-emu.org, git.eden-emu.dev, rwfc.net), damit du siehst, ob es ein
  Update gibt. Das Update selbst lädst du wieder selbst herunter.
- Es gibt keine Telemetrie, keine Konten und keine Datenübertragung an den Autor.
- Emulatoren und Tools von Drittanbietern können eigene Online-Funktionen haben. Dafür gelten deren Einstellungen und Bedingungen.

---

## Aus dem Quellcode bauen

Voraussetzungen: Windows 10/11 x64, [.NET 10 SDK](https://dotnet.microsoft.com/download), PowerShell.

1. Repository klonen:
   ```powershell
   git clone https://github.com/mRbRaIn0/Emulator-PC-Hub.git
   cd Emulator-PC-Hub
   ```
2. **SDL3 bereitstellen (für die volle Controller-Unterstützung):** SDL3 ist ein Drittprojekt und daher nicht im
   Repository. Lade die Windows-x64-Version von https://github.com/libsdl-org/SDL/releases (`SDL3-*-win32-x64.zip`) herunter
   und lege `SDL3.dll` (und optional die Lizenzdatei als `SDL3-LICENSE.txt`) nach
   `src\EmulatorPCHub.Controllers\native\`. Ohne SDL3 baut und startet der Hub trotzdem, nutzt dann aber nur XInput
   (Xbox-kompatible Controller).
3. Bauen (führt die Tests aus und veröffentlicht nach `App\`):
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\build.ps1
   ```
   Ergebnis: `App\Emulator PC Hub.exe` (self-contained, inkl. Windows App SDK). `-SkipTests` überspringt die Tests.

NuGet-Abhängigkeiten (werden beim Build über NuGet bezogen): Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite,
CommunityToolkit.Mvvm, für die Tests xUnit.

---

## Rechtliches

- Emulator PC Hub ist ein unabhängiges Hobbyprojekt. Es steht in keiner Verbindung zu Konsolen- oder Spieleherstellern
  und wird von keinem dieser Unternehmen unterstützt. Alle genannten Marken und Namen gehören ihren jeweiligen Inhabern
  und werden nur zur Beschreibung der Kompatibilität verwendet.
- Das Repository enthält ausschließlich den selbst entwickelten Hub/Launcher und eigene Ressourcen (Code, Icon).
  Es enthält **keine** Spiele, ROMs, BIOS-Dateien, Firmware, Keys, Emulatoren, Mods oder Dateien anderer Projekte.
- Das Release-Paket enthält zusätzlich die zum Betrieb nötigen Laufzeitbibliotheken (.NET, Windows App SDK) und SDL3
  (zlib-Lizenz, Lizenztext liegt bei).
- Du bist selbst dafür verantwortlich, dass du die verwendeten Inhalte rechtmäßig besitzt und nutzt.
