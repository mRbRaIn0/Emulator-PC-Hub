using System.Text.Json;
using System.Text.Json.Serialization;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Mii;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation;
using EmulatorPCHub.Emulation.Cemu;
using EmulatorPCHub.Emulation.Dolphin;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.UI.Services;

/// <summary>Ein Mii in der Sammlung des Hubs (Rohdaten im nativen Format, unverändert).</summary>
public sealed class HubMii
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("data")] public string DataBase64 { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("imported")] public DateTimeOffset Imported { get; set; } = DateTimeOffset.Now;

    [JsonIgnore] public byte[] Data => Convert.FromBase64String(DataBase64);
    [JsonIgnore] public MiiInfo? Info => MiiCodec.Read(Data);
    [JsonIgnore] public string Name => Info?.Name ?? "?";
    [JsonIgnore] public MiiFormat Format => Info?.Format ?? MiiFormat.Wii;
    [JsonIgnore] public string ColorHex => MiiCodec.FavoriteColors[Math.Clamp(Info?.FavoriteColor ?? 0, 0, 11)];
}

/// <summary>
/// Mii Manager: Sammlung eigener Miis (<c>data\miis\miis.json</c>) mit Import aus Dolphin (<c>RFL_DB.dat</c>),
/// Cemu-Konten und Dateien, Export als <c>.mii</c>/<c>.ffsd</c> sowie Zuweisen an Hub-Profile und Emulatoren:
/// Wii → Dolphins Mii-Datenbank, Wii U → Cemu-Konto, Switch → Name des Eden-Benutzers
/// (Switch-Miis selbst entstehen im Mii-Editor von Eden, der die eigene Firmware nutzt).
/// Der Hub zeichnet keine Mii-Gesichter und nutzt keine Nintendo-Grafiken.
/// </summary>
public sealed class MiiService
{
    private readonly string _file;
    private readonly AdapterRegistry _adapters;
    private readonly BackupService _backups;
    private List<HubMii> _miis;

    public event EventHandler? Changed;

    public MiiService(AppPaths paths, AdapterRegistry adapters, BackupService backups)
    {
        _file = Path.Combine(paths.Data, "miis", "miis.json");
        _adapters = adapters;
        _backups = backups;
        _miis = Load();
    }

    public IReadOnlyList<HubMii> All => _miis;

    public HubMii? Find(string? id) => id == null ? null : _miis.FirstOrDefault(m => m.Id == id);

    // ------------------------------------------------------------------
    // Import
    // ------------------------------------------------------------------

    /// <summary>Fügt ein Mii hinzu (Duplikate mit gleichen Rohdaten werden übersprungen).</summary>
    public HubMii? Add(byte[] data, string source)
    {
        if (MiiCodec.Read(data) == null)
            return null;
        var b64 = Convert.ToBase64String(data);
        if (_miis.FirstOrDefault(m => m.DataBase64 == b64) is { } existing)
            return existing;
        var mii = new HubMii { DataBase64 = b64, Source = source };
        _miis.Add(mii);
        Save();
        return mii;
    }

    /// <summary>Importiert eine Mii-Datei (.mii/.rcd = Wii, .ffsd/.cfsd/.bin = Wii U/3DS mit 96 Bytes).</summary>
    public HubMii ImportFile(string file)
    {
        var data = File.ReadAllBytes(file);
        if (data.Length == 0x5C) // 3DS-Mii ohne Prüfsumme (CFSD ohne CRC) → ergänzen
            data = [.. data, 0, 0, 0, 0];
        if (data.Length == MiiCodec.Ver3Size && !MiiCodec.HasValidVer3Crc(data))
            MiiCodec.FixVer3Crc(data);
        return Add(data, $"Datei {Path.GetFileName(file)}")
               ?? throw new InvalidDataException($"„{Path.GetFileName(file)}“ ist kein Mii (erwartet 74 Bytes Wii oder 96 Bytes Wii U/3DS).");
    }

    public string WiiDatabaseFile => WiiMiiDatabase.PathIn(_adapters.Dolphin.UserDirectory());

    /// <summary>Alle Miis aus Dolphins Wii-NAND übernehmen. Gibt die Anzahl neuer Miis zurück.</summary>
    public int ImportFromDolphin()
    {
        var db = WiiMiiDatabase.Load(WiiDatabaseFile);
        if (db == null)
            return 0;
        var before = _miis.Count;
        foreach (var (slot, data) in db.Miis())
            Add(data, $"Dolphin (Wii), Platz {slot + 1}");
        return _miis.Count - before;
    }

    public IReadOnlyList<CemuAccount> CemuAccountsList() => CemuAccounts.List(_adapters.Cemu.MlcPath());

    /// <summary>Miis aller Cemu-Konten übernehmen.</summary>
    public int ImportFromCemu()
    {
        var before = _miis.Count;
        foreach (var acc in CemuAccountsList())
            if (acc.MiiData.Length == MiiCodec.Ver3Size)
                Add(acc.MiiData, $"Cemu (Wii U), Konto {acc.PersistentId}");
        return _miis.Count - before;
    }

    // ------------------------------------------------------------------
    // Bearbeiten / Export
    // ------------------------------------------------------------------

    public void Rename(HubMii mii, string name)
    {
        mii.DataBase64 = Convert.ToBase64String(MiiCodec.WithName(mii.Data, name));
        Save();
    }

    public void Remove(HubMii mii)
    {
        _miis.Remove(mii);
        Save();
    }

    public string Export(HubMii mii, string folder)
    {
        var name = string.Concat(mii.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var target = Path.Combine(folder, (name.Length == 0 ? "Mii" : name) + MiiCodec.FileExtension(mii.Format));
        File.WriteAllBytes(target, mii.Data);
        return target;
    }

    // ------------------------------------------------------------------
    // Zuweisen
    // ------------------------------------------------------------------

    /// <summary>Schreibt ein Wii-Mii in Dolphins Mii-Datenbank (vorher Backup).</summary>
    public string AssignToWii(HubMii mii)
    {
        if (mii.Format != MiiFormat.Wii)
            return "Nur Wii-Miis können in die Wii übernommen werden. Wii-U-/3DS-Miis bitte im Mii-Kanal neu anlegen.";
        var db = WiiMiiDatabase.Load(WiiDatabaseFile);
        if (db == null)
            return "In Dolphin gibt es noch keine Mii-Datenbank. Einmal ein Mii-Spiel (z. B. Mario Kart Wii) oder den " +
                   "Mii-Kanal in Dolphin starten, danach erneut zuweisen.";
        _backups.BackupFile(BackupCategory.Config, WiiDatabaseFile, "dolphin-RFL_DB");
        var slot = db.Put(mii.Data);
        if (slot < 0)
            return "Die Wii-Mii-Datenbank ist voll (100 Miis).";
        db.Save();
        return $"„{mii.Name}“ ist jetzt auf der Wii (Dolphin), Platz {slot + 1}.";
    }

    /// <summary>Setzt das Mii eines Cemu-Kontos (vorher Backup).</summary>
    public string AssignToCemu(HubMii mii, string persistentId)
    {
        // Wii-Miis werden ins Wii-U-Format umgewandelt
        var data = mii.Format == MiiFormat.Wii ? MiiConvert.WiiToVer3(mii.Data) : mii.Data;
        var acc = CemuAccountsList().FirstOrDefault(a => a.PersistentId.Equals(persistentId, StringComparison.OrdinalIgnoreCase));
        if (acc == null)
            return $"Cemu-Konto {persistentId} nicht gefunden (Cemu einmal starten).";
        _backups.BackupFile(BackupCategory.Config, acc.FilePath, $"cemu-account-{persistentId}");
        CemuAccounts.SetMii(acc.FilePath, data, mii.Name);
        return $"„{mii.Name}“ ist jetzt das Mii von Cemu-Konto {persistentId}.";
    }

    public string SwitchDatabaseFile => SwitchMiiDatabase.PathIn(_adapters.Switch.DataDirectory());

    /// <summary>
    /// Switch: Mii (Wii, Wii U oder 3DS) umwandeln und in Edens Mii-Datenbank eintragen (vorher Backup).
    /// Spiele wie Tomodachi Life finden es dort als „Mii auf der Konsole“.
    /// </summary>
    public string AddToSwitch(HubMii mii)
    {
        var file = SwitchDatabaseFile;
        if (File.Exists(file))
            _backups.BackupFile(BackupCategory.Config, file, "eden-MiiDatabase");
        var db = SwitchMiiDatabase.LoadOrCreate(file);
        var slot = db.Add(MiiConvert.ToSwitchStoreData(mii.Data));
        if (slot < 0)
            return "Die Switch-Mii-Datenbank ist voll.";
        db.Save();
        return $"„{mii.Name}“ ist jetzt auf der Switch (Eden), Platz {slot + 1}. Switch-Spiele finden es als Mii auf der Konsole.";
    }

    /// <summary>
    /// Automatischer Abgleich vor Switch-Spielen: alle Miis aus dem Wii-Mii-Kanal (Dolphin) in Edens Mii-Datenbank
    /// übernehmen. Neue Miis kommen hinzu, im Mii-Kanal geänderte werden aktualisiert (feste Erstell-ID je Wii-Mii).
    /// Rückgabe: Anzahl neuer oder geänderter Miis.
    /// </summary>
    public int SyncWiiMiisToSwitch()
    {
        var wii = WiiMiiDatabase.Load(WiiDatabaseFile);
        if (wii == null)
            return 0;
        ImportFromDolphin(); // auch in die Sammlung des Hubs
        var file = SwitchDatabaseFile;
        var db = SwitchMiiDatabase.LoadOrCreate(file);
        var changed = 0;
        foreach (var (_, data) in wii.Miis())
        {
            if (MiiCodec.Read(data) == null)
                continue;
            db.Upsert(MiiConvert.ToSwitchStoreData(data, MiiConvert.StableCreateId(data)), out var c);
            if (c)
                changed++;
        }
        if (changed > 0)
        {
            if (File.Exists(file))
                _backups.BackupFile(BackupCategory.Config, file, "eden-MiiDatabase");
            db.Save();
            HubLog.Info($"Mii-Abgleich: {changed} Wii-Mii(s) in Edens Mii-Datenbank übernommen");
        }
        return changed;
    }

    public IReadOnlyList<EdenUser> EdenUsers() => EdenProfiles.List(_adapters.Switch.DataDirectory());

    /// <summary>Switch: Eden-Benutzer nach dem Mii benennen (das Mii selbst wird im Eden-Mii-Editor angelegt).</summary>
    public string AssignToEden(HubMii mii, string uuid)
    {
        var data = _adapters.Switch.DataDirectory();
        var file = EdenProfiles.ProfilesFile(data);
        if (!File.Exists(file))
            return "Eden hat noch keine Benutzer (Eden einmal starten).";
        _backups.BackupFile(BackupCategory.Config, file, "eden-profiles");
        EdenProfiles.Rename(data, uuid, mii.Name);
        return $"Eden-Benutzer heißt jetzt „{mii.Name}“.";
    }

    // ------------------------------------------------------------------

    private List<HubMii> Load()
    {
        try
        {
            return File.Exists(_file) ? JsonSerializer.Deserialize<List<HubMii>>(File.ReadAllText(_file), HubJson.Options) ?? [] : [];
        }
        catch (JsonException ex)
        {
            HubLog.Warn("miis.json ist beschädigt", ex);
            return [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(_miis, HubJson.Options));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
