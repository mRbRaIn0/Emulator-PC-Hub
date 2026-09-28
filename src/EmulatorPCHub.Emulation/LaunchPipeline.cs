using System.Diagnostics;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation;

/// <summary>Fenster-Steuerung des Hubs während ein Spiel läuft (implementiert von der App).</summary>
public interface IHubWindow
{
    void HideForGame();
    void RestoreAfterGame();
}

public enum LaunchStep
{
    LoadPreset,
    CheckFiles,
    LoadControllerProfile,
    ApplyPreset,
    StartProcess,
    HideHub,
    Running,
    SavePlaytime,
    RestoreHub,
    Done,
    Failed,
}

public sealed record LaunchProgress(LaunchStep Step, string Text);

public sealed class LaunchOutcome
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int? ExitCode { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset Started { get; init; }
    public required LaunchLogEntry Log { get; init; }
}

/// <summary>
/// Game-Launch-Pipeline (Plan Abschnitt 18):
/// Preset laden → Dateien prüfen → Controllerprofil → Preset/Mods aktivieren → Emulator starten →
/// Hub ausblenden → Spiel läuft → Spielzeit speichern → Hub wiederherstellen.
/// </summary>
public sealed class LaunchPipeline
{
    private readonly LaunchLog _launchLog;
    private Process? _process;
    private readonly HashSet<int> _children = [];
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }
    /// <summary>Der Spielprozess läuft (nach dem Start, vor dem Aufräumen) – Controller gehören dann dem Spiel.</summary>
    public bool GameProcessRunning { get; private set; }
    public GameEntry? CurrentGame { get; private set; }

    public LaunchPipeline(LaunchLog launchLog)
    {
        _launchLog = launchLog;
    }

    /// <param name="applyPreset">Aktiviert Mods/Konfiguration des Presets (vor dem Start).</param>
    /// <param name="recordSession">Speichert die Spielzeit.</param>
    public async Task<LaunchOutcome> RunAsync(
        LaunchRequest request,
        IEmulatorAdapter adapter,
        IHubWindow? window,
        IProgress<LaunchProgress>? progress,
        Func<Task>? applyPreset,
        Action<DateTimeOffset, TimeSpan, int?>? recordSession,
        CancellationToken ct = default,
        Func<Task>? loadControllers = null,
        Func<Task>? afterGame = null)
    {
        var log = new LaunchLogEntry
        {
            Game = request.Game.Title,
            GameId = request.Game.Id,
            Preset = request.Preset?.Name,
            Emulator = adapter.DisplayName,
        };
        void Step(LaunchStep s, string text)
        {
            log.Steps.Add(text);
            progress?.Report(new LaunchProgress(s, text));
        }

        var started = DateTimeOffset.Now;
        var hidden = false;
        if (IsRunning)
            return Fail("Es läuft bereits ein Spiel.");

        IsRunning = true;
        CurrentGame = request.Game;
        try
        {
            Step(LaunchStep.LoadPreset, $"Preset laden: {request.Preset?.Name ?? "Standard"}");
            Step(LaunchStep.CheckFiles, "Dateien prüfen");
            var spec = await Task.Run(() => adapter.PrepareLaunch(request), ct);
            log.Executable = spec.FileName;
            log.Arguments = spec.ArgumentString;

            Step(LaunchStep.LoadControllerProfile, "Controllerprofil laden");
            if (loadControllers != null)
                await loadControllers();
            if (applyPreset != null)
            {
                Step(LaunchStep.ApplyPreset, $"{request.Preset?.Name ?? "Preset"} aktivieren");
                await applyPreset();
            }

            Step(LaunchStep.StartProcess, $"{adapter.DisplayName} starten");
            var psi = new ProcessStartInfo(spec.FileName)
            {
                WorkingDirectory = spec.WorkingDirectory ?? Path.GetDirectoryName(spec.FileName),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in spec.Arguments)
                psi.ArgumentList.Add(a);
            foreach (var (k, v) in spec.Environment)
                psi.Environment[k] = v;
            HubLog.Info($"Start: {spec.FileName} {spec.ArgumentString}");

            var process = Process.Start(psi) ?? throw new LaunchException($"{adapter.DisplayName} konnte nicht gestartet werden.");
            lock (_lock)
            {
                _process = process;
                _children.Clear();
            }
            started = DateTimeOffset.Now;
            GameProcessRunning = true;
            if (spec.AfterStart != null)
                _ = Task.Run(() => spec.AfterStart(process, ct), ct);

            Step(LaunchStep.HideHub, "Hub ausblenden");
            window?.HideForGame();
            hidden = true;

            Step(LaunchStep.Running, $"{request.Game.Title} läuft");
            await WaitForGameAsync(process, spec.FollowChildProcesses, ct);
            var exitCode = SafeExitCode(process);
            var duration = DateTimeOffset.Now - started;

            Step(LaunchStep.SavePlaytime, $"Spielzeit speichern ({(int)duration.TotalMinutes} min)");
            recordSession?.Invoke(started, duration, exitCode);

            log.ExitCode = exitCode;
            log.DurationSeconds = duration.TotalSeconds;
            if (exitCode is { } code && IsCrashCode(code))
                log.Error = $"{adapter.DisplayName} ist abgestürzt ({CrashText(code)}) nach {(int)duration.TotalSeconds} s. Details in Logs.";
            else if (duration < TimeSpan.FromSeconds(4) && exitCode is not null and not 0)
                log.Error = $"{adapter.DisplayName} wurde sofort beendet (Exit-Code {exitCode}). Details in Logs.";

            Step(LaunchStep.RestoreHub, "Zurück zum Hub");
            return new LaunchOutcome
            {
                Success = log.Error == null,
                Error = log.Error,
                ExitCode = exitCode,
                Duration = duration,
                Started = started,
                Log = log,
            };
        }
        catch (LaunchException ex)
        {
            return Fail(ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Fail("Start abgebrochen.");
        }
        catch (Exception ex)
        {
            HubLog.Error("Spielstart fehlgeschlagen", ex);
            return Fail($"Start fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            GameProcessRunning = false;
            if (afterGame != null)
            {
                try { await afterGame(); }
                catch (Exception ex) { HubLog.Warn("Aufräumen nach dem Spiel fehlgeschlagen", ex); }
            }
            if (hidden)
                window?.RestoreAfterGame();
            lock (_lock)
                _process = null;
            IsRunning = false;
            CurrentGame = null;
            _launchLog.Append(log);
        }

        LaunchOutcome Fail(string message)
        {
            log.Error = message;
            progress?.Report(new LaunchProgress(LaunchStep.Failed, message));
            HubLog.Warn($"Start von {request.Game.Title}: {message}");
            return new LaunchOutcome { Success = false, Error = message, Log = log, Started = started };
        }
    }

    /// <summary>Wartet auf das Spielende – auch wenn der Emulator über einen Launcher-Prozess gestartet wird.</summary>
    private async Task WaitForGameAsync(Process process, bool followChildren, CancellationToken ct)
    {
        while (!process.HasExited)
        {
            if (followChildren)
                TrackChildren(process.Id);
            try
            {
                await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(1), ct);
            }
            catch (TimeoutException)
            {
            }
        }

        if (!followChildren)
            return;
        // Kindprozesse, die weiterlaufen (z. B. Spiel-EXE nach Launcher-Stub)
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            List<Process> alive;
            lock (_lock)
                alive = _children.Select(TryGet).Where(p => p is { HasExited: false }).Cast<Process>().ToList();
            if (alive.Count == 0)
                return;
            foreach (var p in alive)
                TrackChildren(p.Id);
            await Task.Delay(1000, ct);
        }
    }

    private void TrackChildren(int pid)
    {
        var snapshot = ProcessTree.Snapshot();
        lock (_lock)
        {
            foreach (var c in ProcessTree.Descendants(pid, snapshot))
                _children.Add(c);
        }
    }

    private static Process? TryGet(int pid)
    {
        try { return Process.GetProcessById(pid); }
        catch (ArgumentException) { return null; }
    }

    /// <summary>Windows-Ausnahmecodes (NTSTATUS 0xC0000000…), z. B. Zugriffsverletzung 0xC0000005.</summary>
    public static bool IsCrashCode(int code) => (uint)code >= 0xC0000000u;

    public static string CrashText(int code) => (uint)code switch
    {
        0xC0000005u => "Zugriffsverletzung 0xC0000005",
        0xC0000409u => "Stack-Pufferüberlauf 0xC0000409",
        0xC00000FDu => "Stapelüberlauf 0xC00000FD",
        0xC0000374u => "Heap-Beschädigung 0xC0000374",
        _ => $"Ausnahme 0x{(uint)code:X8}",
    };

    private static int? SafeExitCode(Process p)
    {
        try { return p.HasExited ? p.ExitCode : null; }
        catch (InvalidOperationException) { return null; }
    }

    /// <summary>Beendet das laufende Spiel inklusive aller Kindprozesse (z. B. über Home + Minus).</summary>
    public void StopCurrent()
    {
        Process? p;
        int[] children;
        lock (_lock)
        {
            p = _process;
            children = _children.ToArray();
        }
        try
        {
            if (p is { HasExited: false })
                p.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Spielprozess konnte nicht beendet werden", ex);
        }
        foreach (var pid in children)
        {
            try { TryGet(pid)?.Kill(entireProcessTree: true); }
            catch { }
        }
    }
}
