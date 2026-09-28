using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EmulatorPCHub.Core;
using EmulatorPCHub.Core.Config;
using EmulatorPCHub.Core.Logging;
using EmulatorPCHub.Core.Models;
using EmulatorPCHub.Emulation.WheelWizard;

namespace EmulatorPCHub.Emulation.WiiCompiled;

public sealed record WiiCompiledInstall(string InstallDir, string SetupExe, string? Version, bool IsHubManaged);

public sealed record WiiCompiledProgress(double? Percent, string Message);

/// <summary>
/// WiiCompiled – native PC-Version von Mario Kart Wii (statische Rekompilierung, Plan 6.2).
/// Der Hub nutzt das offizielle Setup-Programm und dessen Kommandozeilen-Vertrag:
/// <c>--launch-base</c>, <c>--launch-retro</c>, <c>--silent --game … --install-dir …</c>, <c>--version</c>.
/// </summary>
public sealed class WiiCompiledAdapter : AdapterBase
{
    public const string SetupFileName = "WiiCompiled-Setup.exe";
    private readonly WheelWizardIntegration _wheelWizard;

    public WiiCompiledAdapter(AppPaths paths, ConfigService config, WheelWizardIntegration wheelWizard) : base(paths, config)
    {
        _wheelWizard = wheelWizard;
    }

    public override string Id => EmulatorIds.WiiCompiled;
    public override string DisplayName => "WiiCompiled";
    public override IReadOnlyList<HubPlatform> Platforms { get; } = [HubPlatform.Wii];

    /// <summary>Installationsordner, den der Hub selbst verwaltet (portables Layout).</summary>
    public string HubInstallDir => Path.Combine(Paths.IntegrationDir("wiicompiled"), "Install");

    /// <summary>Heruntergeladenes Setup, mit dem der Hub installieren kann.</summary>
    public string? DownloadedSetup
    {
        get
        {
            var p = Path.Combine(Paths.IntegrationDir("wiicompiled"), SetupFileName);
            return File.Exists(p) ? p : null;
        }
    }

    public WiiCompiledInstall? FindInstall()
    {
        var candidates = new List<(string dir, bool hub)>();
        if (!string.IsNullOrWhiteSpace(Config.Current.Emulators.WiiCompiled))
        {
            var c = Config.Current.Emulators.WiiCompiled;
            candidates.Add((File.Exists(c) ? Path.GetDirectoryName(c)! : c, false));
        }
        candidates.Add((HubInstallDir, true));
        candidates.Add((Path.Combine(_wheelWizard.DataFolder(), "Recomp", "Install"), false));
        candidates.Add((Path.Combine(AppData, "CT-MKWII", "Recomp", "Install"), false));

        foreach (var (dir, hub) in candidates)
        {
            var setup = Path.Combine(dir, SetupFileName);
            var state = Path.Combine(dir, "install-state.json");
            if (File.Exists(setup) && File.Exists(state))
                return new WiiCompiledInstall(dir, setup, ReadVersion(state), hub);
        }
        return null;
    }

    private static string? ReadVersion(string stateFile)
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(stateFile), new JsonNodeOptions { PropertyNameCaseInsensitive = true });
            return node?["setupVersion"]?.GetValue<string>() ?? node?["version"]?.GetValue<string>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public override EmulatorInstallation DetectInstallation()
    {
        var install = FindInstall();
        if (install == null)
        {
            var result = new EmulatorInstallation();
            if (DownloadedSetup != null)
                result.Notes.Add("Setup heruntergeladen – Installation benötigt deinen Mario-Kart-Wii-PAL-Dump (RMCP01).");
            return result;
        }
        return new EmulatorInstallation
        {
            ExecutablePath = install.SetupExe,
            Version = install.Version,
            UserDataDir = Path.Combine(Path.GetDirectoryName(install.InstallDir)!, "UserData"),
            Portable = true,
        };
    }

    public override IReadOnlyList<string> DetectGames() => [];

    public override LaunchSpec PrepareLaunch(LaunchRequest request)
    {
        var install = FindInstall()
                      ?? throw new LaunchException("WiiCompiled ist noch nicht installiert. Auf der Mario-Kart-Wii-Seite → „WiiCompiled installieren“.");
        var retro = request.Preset?.Kind == PresetKind.RetroRewind;
        return new LaunchSpec
        {
            FileName = install.SetupExe,
            Arguments = [retro ? "--launch-retro" : "--launch-base"],
            WorkingDirectory = install.InstallDir,
            Description = retro ? $"WiiCompiled {install.Version}: Retro Rewind" : $"WiiCompiled {install.Version}: Mario Kart Wii",
        };
    }

    /// <summary>Prüft, ob ein Dump für WiiCompiled geeignet ist (PAL RMCP01).</summary>
    public static string? ValidateDump(GameEntry? game)
    {
        if (game == null || string.IsNullOrEmpty(game.Path) || !File.Exists(game.Path))
            return "Kein Mario-Kart-Wii-Dump gefunden. Lege deinen eigenen Dump in einen Bibliotheksordner oder wähle ihn aus.";
        if (!string.Equals(game.GameCode, "RMCP01", StringComparison.OrdinalIgnoreCase))
            return $"WiiCompiled benötigt die PAL-Version (RMCP01). Gefunden: {game.GameCode ?? "unbekannt"}. Dolphin funktioniert mit allen Regionen.";
        return null;
    }

    /// <summary>
    /// Installiert WiiCompiled über das offizielle Setup (still, portabel). Braucht viel Zeit und ~20 GB freien Platz.
    /// </summary>
    public async Task<int> InstallAsync(string gameFile, string? retroRewindDir, IProgress<WiiCompiledProgress>? progress,
        CancellationToken ct)
    {
        var setup = DownloadedSetup ?? FindInstall()?.SetupExe
                    ?? throw new LaunchException("WiiCompiled-Setup.exe wurde nicht gefunden (Komponenten).");
        var root = Paths.IntegrationDir("wiicompiled");
        Directory.CreateDirectory(root);
        var args = new List<string> { "--silent", "--game", gameFile, "--install-dir", HubInstallDir, "--portable", "--progress-json" };
        if (!string.IsNullOrWhiteSpace(retroRewindDir) && Directory.Exists(retroRewindDir))
        {
            args.Add("--retro-dir");
            args.Add(retroRewindDir);
            // Kein "--download-retro-wfc-payload": Der Hub lässt keine Fremddateien automatisch herunterladen.
        }
        return await RunSetupAsync(setup, args, root, progress, ct);
    }

    public async Task<string?> QueryVersionAsync(CancellationToken ct = default)
    {
        var setup = FindInstall()?.SetupExe ?? DownloadedSetup;
        if (setup == null)
            return null;
        var output = new List<string>();
        await RunSetupAsync(setup, ["--version"], Path.GetDirectoryName(setup)!,
            new Progress<WiiCompiledProgress>(p => output.Add(p.Message)), ct);
        return output.LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
    }

    private static async Task<int> RunSetupAsync(string exe, IReadOnlyList<string> args, string workDir,
        IProgress<WiiCompiledProgress>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        HubLog.Info($"WiiCompiled-Setup: {string.Join(' ', args)}");
        using var p = Process.Start(psi) ?? throw new LaunchException("WiiCompiled-Setup konnte nicht gestartet werden.");
        void Handle(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;
            progress?.Report(ParseProgress(line));
        }
        p.OutputDataReceived += (_, e) => Handle(e.Data);
        p.ErrorDataReceived += (_, e) => Handle(e.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        HubLog.Info($"WiiCompiled-Setup beendet mit Code {p.ExitCode}");
        return p.ExitCode;
    }

    /// <summary>Liest NDJSON-Fortschritt (--progress-json); unbekannte Zeilen werden als Text weitergegeben.</summary>
    public static WiiCompiledProgress ParseProgress(string line)
    {
        if (line.TrimStart().StartsWith('{'))
        {
            try
            {
                var node = JsonNode.Parse(line) as JsonObject;
                double? percent = null;
                foreach (var key in new[] { "percent", "progress", "pct" })
                {
                    if (node?[key] is JsonValue v && v.TryGetValue<double>(out var d))
                    {
                        percent = d <= 1 && key == "progress" ? d * 100 : d;
                        break;
                    }
                }
                var msg = node?["message"]?.ToString() ?? node?["step"]?.ToString() ?? node?["event"]?.ToString() ?? line;
                return new WiiCompiledProgress(percent, msg);
            }
            catch (JsonException)
            {
            }
        }
        return new WiiCompiledProgress(null, line);
    }
}
