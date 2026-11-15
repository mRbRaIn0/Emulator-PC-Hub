using EmulatorPCHub.Core.Backup;
using EmulatorPCHub.Core.Input;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.Switch;

namespace EmulatorPCHub.Emulation.Input;

/// <summary>
/// Eglantine ist ein Eden-Fork mit derselben <c>qt-config.ini</c> ([Controls] → <c>player_N_…</c>); die Belegung
/// wird deshalb von <see cref="EdenInputWriter"/> übernommen. Eine Spiel-Konfiguration (<c>config\custom</c>) überschreibt
/// die Spieler nicht, die globale Datei gilt also auch für Mario Kart World.
/// </summary>
public sealed class EglantineInputWriter : IInputConfigWriter
{
    private readonly string _configFile;
    private readonly BackupService? _backups;

    public EglantineInputWriter(string configFile, BackupService? backups)
    {
        _configFile = configFile;
        _backups = backups;
    }

    public string Apply(InputSetup setup, GameEntry game)
    {
        var notes = new List<string>();
        var ini = EdenIni.Load(_configFile);
        EdenInputWriter.Write(ini, setup, notes);
        if (File.Exists(_configFile))
            _backups?.BackupFile(BackupCategory.Config, _configFile, "eglantine-qt-config");
        ini.Save(_configFile);
        var text = $"Eglantine: {setup.Players.Count} Spieler konfiguriert";
        return notes.Count == 0 ? text : text + " (" + string.Join("; ", notes) + ")";
    }
}
