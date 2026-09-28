using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation;

/// <summary>Ergebnis der Installationserkennung eines Emulators/Backends.</summary>
public sealed class EmulatorInstallation
{
    public bool IsInstalled => ExecutablePath != null && File.Exists(ExecutablePath);
    public string? ExecutablePath { get; init; }
    public string? Version { get; init; }
    /// <summary>Benutzer-/Datenordner des Emulators (Config, Saves, Mods).</summary>
    public string? UserDataDir { get; init; }
    public bool Portable { get; init; }
    public List<string> Problems { get; } = [];
    public List<string> Notes { get; } = [];

    public static EmulatorInstallation NotFound { get; } = new();
}

/// <summary>Was genau gestartet werden soll.</summary>
public sealed class LaunchSpec
{
    public required string FileName { get; init; }
    public List<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public Dictionary<string, string> Environment { get; init; } = [];
    /// <summary>Menschlich lesbare Beschreibung (für Log/Overlay).</summary>
    public string Description { get; init; } = "";
    /// <summary>Wenn der gestartete Prozess sofort endet, auf Kindprozesse warten (Launcher-Stubs).</summary>
    public bool FollowChildProcesses { get; init; } = true;
    /// <summary>Läuft nach dem Prozessstart nebenher (z. B. Hilfsfenster des Emulators öffnen).</summary>
    public Func<System.Diagnostics.Process, CancellationToken, Task>? AfterStart { get; init; }

    public string ArgumentString => string.Join(" ", Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}

public sealed class LaunchRequest
{
    public required GameEntry Game { get; init; }
    public GamePreset? Preset { get; init; }
    public bool Fullscreen { get; init; } = true;
}

/// <summary>
/// Emulator-Abstraktionsschicht (Plan Abschnitt 10). Das UI spricht nie direkt mit Emulatoren,
/// sondern nur über Adapter – dadurch ist z. B. der Switch-Emulator austauschbar.
/// </summary>
public interface IEmulatorAdapter
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyList<HubPlatform> Platforms { get; }

    EmulatorInstallation DetectInstallation();

    /// <summary>Ordner, die im Emulator selbst als Spieleordner eingetragen sind.</summary>
    IReadOnlyList<string> DetectGames();

    /// <summary>Bereitet den Start vor (Konfiguration/Mods) und liefert die Startparameter.</summary>
    LaunchSpec PrepareLaunch(LaunchRequest request);

    string? GetVersion() => DetectInstallation().Version;

    IReadOnlyDictionary<string, string> GetConfig();

    void ApplyConfig(IReadOnlyDictionary<string, string> values);

    /// <summary>Ordner, in dem aktive Mods für ein Spiel liegen (null = Mods nicht unterstützt).</summary>
    string? GetModDirectory(string gameKey) => null;
}
