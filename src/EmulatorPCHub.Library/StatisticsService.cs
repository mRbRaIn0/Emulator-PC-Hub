using System.Globalization;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

/// <summary>Kennzahlen eines Spiels (oder aller Spiele) aus den gespeicherten Sessions.</summary>
public sealed record GameStats(
    string GameId,
    string Title,
    HubPlatform Platform,
    int Sessions,
    int Starts,
    int FailedStarts,
    TimeSpan Total,
    TimeSpan Average,
    TimeSpan Longest,
    DateTimeOffset? FirstPlayed,
    DateTimeOffset? LastStart);

/// <summary>Ein Balken im Verlauf (Tag bzw. Woche).</summary>
public sealed record StatBucket(string Label, DateTime Start, TimeSpan Total);

public sealed record NamedTotal(string Key, TimeSpan Total, int Sessions);

/// <summary>Gesamtauswertung für die Statistik-Seite.</summary>
public sealed class StatsOverview
{
    public TimeSpan Total { get; init; }
    public int Sessions { get; init; }
    public int Starts { get; init; }
    public TimeSpan Average { get; init; }
    public TimeSpan Longest { get; init; }
    public string? LongestGame { get; init; }
    public DateTimeOffset? LastStart { get; init; }
    public NamedTotal? BestWeek { get; init; }
    public NamedTotal? BestMonth { get; init; }
    public List<NamedTotal> ByPlatform { get; init; } = [];
    public List<NamedTotal> ByProfile { get; init; } = [];
    public List<NamedTotal> ByPreset { get; init; } = [];
    public List<GameStats> Games { get; init; } = [];
    public List<StatBucket> Last30Days { get; init; } = [];
    public List<StatBucket> Last12Weeks { get; init; } = [];
}

/// <summary>
/// Spielestatistik (Sessions, Starts, Durchschnitt, längste Session, meistgespielte Woche/Monat,
/// Spielzeit nach Plattform/Profil/Preset und Verlauf). Rechnet nur mit lokalen Daten der Bibliothek.
/// </summary>
public sealed class StatisticsService
{
    private readonly LibraryService _library;

    public StatisticsService(LibraryService library)
    {
        _library = library;
    }

    /// <summary>Auswertung; <paramref name="profileId"/> = nur dieses Profil (null = alle).</summary>
    public StatsOverview Overview(string? profileId = null, DateTime? today = null)
    {
        var games = _library.Games.ToDictionary(g => g.Id);
        var sessions = _library.AllSessions().Where(s => profileId == null || s.ProfileId == profileId).ToList();
        var launches = _library.AllLaunches().Where(l => profileId == null || l.ProfileId == profileId).ToList();
        return Build(sessions, launches, games, today ?? DateTime.Today);
    }

    public GameStats ForGame(GameEntry game, string? profileId = null) =>
        Overview(profileId).Games.FirstOrDefault(g => g.GameId == game.Id)
        ?? new GameStats(game.Id, game.Title, game.Platform, 0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, null, null);

    public static StatsOverview Build(IReadOnlyList<PlaySession> sessions, IReadOnlyList<LaunchAttempt> launches,
        IReadOnlyDictionary<string, GameEntry> games, DateTime today)
    {
        static TimeSpan Sum(IEnumerable<PlaySession> s) => TimeSpan.FromSeconds(s.Sum(x => x.DurationSeconds));
        static TimeSpan Avg(IReadOnlyCollection<PlaySession> s) => s.Count == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(s.Average(x => x.DurationSeconds));

        var perGame = new List<GameStats>();
        foreach (var id in sessions.Select(s => s.GameId).Concat(launches.Select(l => l.GameId)).Distinct())
        {
            var s = sessions.Where(x => x.GameId == id).ToList();
            var l = launches.Where(x => x.GameId == id).ToList();
            games.TryGetValue(id, out var g);
            var lastStart = s.Select(x => (DateTimeOffset?)x.Started).Concat(l.Select(x => (DateTimeOffset?)x.Started)).Max();
            perGame.Add(new GameStats(id, g?.Title ?? id, g?.Platform ?? HubPlatform.BuiltIn,
                s.Count,
                // Ältere Sessions (vor der Start-Zählung) zählen als erfolgreicher Start
                Math.Max(l.Count, s.Count),
                l.Count(x => !x.Success),
                Sum(s), Avg(s),
                s.Count == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(s.Max(x => x.DurationSeconds)),
                s.Count == 0 ? null : s.Min(x => x.Started),
                lastStart));
        }

        var longest = sessions.MaxBy(s => s.DurationSeconds);
        NamedTotal? Best(Func<PlaySession, string> key) => sessions
            .GroupBy(key)
            .Select(g => new NamedTotal(g.Key, Sum(g), g.Count()))
            .MaxBy(x => x.Total);

        List<NamedTotal> Group(Func<PlaySession, string> key) => sessions
            .GroupBy(key)
            .Select(g => new NamedTotal(g.Key, Sum(g), g.Count()))
            .OrderByDescending(x => x.Total)
            .ToList();

        var days = new List<StatBucket>();
        for (var i = 29; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            days.Add(new StatBucket(d.ToString("dd.MM", CultureInfo.InvariantCulture), d,
                Sum(sessions.Where(s => s.Started.LocalDateTime.Date == d))));
        }
        var weeks = new List<StatBucket>();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        for (var i = 11; i >= 0; i--)
        {
            var start = monday.AddDays(-7 * i);
            var end = start.AddDays(7);
            weeks.Add(new StatBucket($"KW {ISOWeek.GetWeekOfYear(start)}", start,
                Sum(sessions.Where(s => s.Started.LocalDateTime >= start && s.Started.LocalDateTime < end))));
        }

        return new StatsOverview
        {
            Total = Sum(sessions),
            Sessions = sessions.Count,
            Starts = perGame.Sum(g => g.Starts),
            Average = Avg(sessions),
            Longest = longest == null ? TimeSpan.Zero : TimeSpan.FromSeconds(longest.DurationSeconds),
            LongestGame = longest == null ? null : (games.TryGetValue(longest.GameId, out var lg) ? lg.Title : longest.GameId),
            LastStart = perGame.Max(g => g.LastStart),
            BestWeek = Best(s => $"KW {ISOWeek.GetWeekOfYear(s.Started.LocalDateTime)}/{ISOWeek.GetYear(s.Started.LocalDateTime)}"),
            BestMonth = Best(s => s.Started.LocalDateTime.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("de-DE"))),
            ByPlatform = Group(s => games.TryGetValue(s.GameId, out var g) ? g.Platform.DisplayName() : "Unbekannt"),
            ByProfile = Group(s => s.ProfileId ?? ""),
            ByPreset = Group(s => (games.TryGetValue(s.GameId, out var g) ? g.Title : s.GameId) + " · " + (string.IsNullOrWhiteSpace(s.Preset) ? "Standard" : s.Preset)),
            Games = perGame.OrderByDescending(g => g.Total).ThenByDescending(g => g.LastStart).ToList(),
            Last30Days = days,
            Last12Weeks = weeks,
        };
    }
}
