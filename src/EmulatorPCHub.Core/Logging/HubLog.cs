using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmulatorPCHub.Core.Logging;

/// <summary>Einfaches Datei-Log unter <c>Logs/</c> (Plan Abschnitt 28).</summary>
public static class HubLog
{
    private static readonly object Lock = new();
    private static string? _dir;
    private static readonly ConcurrentQueue<string> Recent = new();

    public static void Initialize(string logDirectory)
    {
        _dir = logDirectory;
        Directory.CreateDirectory(logDirectory);
        Info($"Emulator PC Hub gestartet ({Environment.OSVersion}, .NET {Environment.Version})");
    }

    public static string? LogDirectory => _dir;

    public static IReadOnlyCollection<string> RecentLines => Recent.ToArray();

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        if (ex != null)
            line += Environment.NewLine + "    " + ex.GetType().Name + ": " + ex.Message;

        Recent.Enqueue(line);
        while (Recent.Count > 300 && Recent.TryDequeue(out _)) { }

        System.Diagnostics.Debug.WriteLine(line);
        if (_dir == null)
            return;
        try
        {
            lock (Lock)
            {
                File.AppendAllText(Path.Combine(_dir, $"hub-{DateTime.Now:yyyyMMdd}.log"),
                    line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging darf nie die App abstürzen lassen.
        }
    }
}

/// <summary>Ein Eintrag pro Spielstart (Spiel, Preset, Emulator, Argumente, Fehler, Exit-Code, Laufzeit).</summary>
public sealed class LaunchLogEntry
{
    [JsonPropertyName("time")] public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    [JsonPropertyName("game")] public string Game { get; set; } = "";
    [JsonPropertyName("gameId")] public string GameId { get; set; } = "";
    [JsonPropertyName("preset")] public string? Preset { get; set; }
    [JsonPropertyName("emulator")] public string Emulator { get; set; } = "";
    [JsonPropertyName("executable")] public string? Executable { get; set; }
    [JsonPropertyName("arguments")] public string? Arguments { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("exitCode")] public int? ExitCode { get; set; }
    [JsonPropertyName("durationSeconds")] public double DurationSeconds { get; set; }
    [JsonPropertyName("steps")] public List<string> Steps { get; set; } = [];

    public bool Succeeded => Error == null;
}

public sealed class LaunchLog
{
    private readonly string _file;
    private readonly object _lock = new();

    public LaunchLog(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _file = Path.Combine(logDirectory, "launches.jsonl");
    }

    public string FilePath => _file;

    public void Append(LaunchLogEntry entry)
    {
        try
        {
            lock (_lock)
                File.AppendAllText(_file, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            HubLog.Warn("Start-Log konnte nicht geschrieben werden", ex);
        }
    }

    public IReadOnlyList<LaunchLogEntry> ReadRecent(int max = 100)
    {
        if (!File.Exists(_file))
            return [];
        var result = new List<LaunchLogEntry>();
        try
        {
            string[] lines;
            lock (_lock)
                lines = File.ReadAllLines(_file);
            foreach (var line in Enumerable.Reverse(lines).Take(max))
            {
                try
                {
                    var e = JsonSerializer.Deserialize<LaunchLogEntry>(line);
                    if (e != null)
                        result.Add(e);
                }
                catch (JsonException)
                {
                }
            }
        }
        catch (IOException)
        {
        }
        return result;
    }
}
