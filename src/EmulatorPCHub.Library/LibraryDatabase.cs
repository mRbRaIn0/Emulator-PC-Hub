using System.Globalization;
using Microsoft.Data.Sqlite;
using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Library;

public sealed record PlaySession(string GameId, string? Preset, string Emulator, DateTimeOffset Started, double DurationSeconds, int? ExitCode,
    string? ProfileId = null);

/// <summary>Ein Startversuch (auch fehlgeschlagene) – für „Starts“ in der Statistik.</summary>
public sealed record LaunchAttempt(string GameId, string? ProfileId, DateTimeOffset Started, bool Success);

/// <summary>Lokale Metadatenbank <c>data/library.db</c> (Plan Abschnitt 12).</summary>
public sealed class LibraryDatabase
{
    private readonly string _connectionString;

    public LibraryDatabase(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = true }.ToString();
        Migrate();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    private void Migrate()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                platform INTEGER NOT NULL,
                path TEXT NOT NULL DEFAULT '',
                game_code TEXT,
                cover TEXT,
                background TEXT,
                icon TEXT,
                emulator TEXT NOT NULL,
                active_preset TEXT,
                last_played TEXT,
                playtime INTEGER NOT NULL DEFAULT 0,
                favorite INTEGER NOT NULL DEFAULT 0,
                controller_profile TEXT,
                graphics_profile TEXT,
                save_path TEXT,
                special INTEGER NOT NULL DEFAULT 0,
                added_at TEXT NOT NULL,
                file_size INTEGER NOT NULL DEFAULT 0,
                placeholder INTEGER NOT NULL DEFAULT 0,
                missing INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                game_id TEXT NOT NULL,
                preset TEXT,
                emulator TEXT NOT NULL,
                started TEXT NOT NULL,
                duration REAL NOT NULL,
                exit_code INTEGER
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_sessions_game ON sessions(game_id);
            CREATE TABLE IF NOT EXISTS favorites (
                profile_id TEXT NOT NULL,
                game_id TEXT NOT NULL,
                PRIMARY KEY (profile_id, game_id)
            );
            CREATE TABLE IF NOT EXISTS launches (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                game_id TEXT NOT NULL,
                profile_id TEXT,
                started TEXT NOT NULL,
                success INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Spalten, die in späteren Versionen dazugekommen sind
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = c.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(games)";
            using var r = info.ExecuteReader();
            while (r.Read())
                columns.Add(r.GetString(1));
        }
        foreach (var (name, sql) in new[] { ("custom_title", "INTEGER NOT NULL DEFAULT 0"), ("hidden", "INTEGER NOT NULL DEFAULT 0"), ("lang_data", "TEXT") })
        {
            if (columns.Contains(name))
                continue;
            using var alter = c.CreateCommand();
            alter.CommandText = $"ALTER TABLE games ADD COLUMN {name} {sql}";
            alter.ExecuteNonQuery();
        }
        var sessionColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = c.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(sessions)";
            using var r = info.ExecuteReader();
            while (r.Read())
                sessionColumns.Add(r.GetString(1));
        }
        if (!sessionColumns.Contains("profile_id"))
        {
            using var alter = c.CreateCommand();
            alter.CommandText = "ALTER TABLE sessions ADD COLUMN profile_id TEXT";
            alter.ExecuteNonQuery();
        }
    }

    public List<GameEntry> LoadGames(bool includeMissing = false)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT * FROM games" + (includeMissing ? "" : " WHERE missing = 0");
        using var r = cmd.ExecuteReader();
        var list = new List<GameEntry>();
        while (r.Read())
        {
            string? S(string col) => r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetString(r.GetOrdinal(col));
            list.Add(new GameEntry
            {
                Id = S("id")!,
                Title = S("title")!,
                Platform = (HubPlatform)r.GetInt32(r.GetOrdinal("platform")),
                Path = S("path") ?? "",
                GameCode = S("game_code"),
                CoverPath = S("cover"),
                BackgroundPath = S("background"),
                IconPath = S("icon"),
                EmulatorId = S("emulator")!,
                ActivePresetId = S("active_preset"),
                LastPlayed = ParseDate(S("last_played")),
                PlayTimeSeconds = r.GetInt64(r.GetOrdinal("playtime")),
                IsFavorite = r.GetInt32(r.GetOrdinal("favorite")) != 0,
                ControllerProfile = S("controller_profile"),
                GraphicsProfile = S("graphics_profile"),
                SavePath = S("save_path"),
                Special = (SpecialPage)r.GetInt32(r.GetOrdinal("special")),
                AddedAt = ParseDate(S("added_at")) ?? DateTimeOffset.Now,
                FileSize = r.GetInt64(r.GetOrdinal("file_size")),
                IsPlaceholder = r.GetInt32(r.GetOrdinal("placeholder")) != 0,
                CustomTitle = r.GetInt32(r.GetOrdinal("custom_title")) != 0,
                HiddenOnHome = r.GetInt32(r.GetOrdinal("hidden")) != 0,
                Languages = ParseLanguages(S("lang_data")),
            });
        }
        return list;
    }

    public void Upsert(GameEntry g)
    {
        using var c = Open();
        Upsert(c, null, g);
    }

    public void UpsertMany(IEnumerable<GameEntry> games)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var g in games)
            Upsert(c, tx, g);
        tx.Commit();
    }

    private static void Upsert(SqliteConnection c, SqliteTransaction? tx, GameEntry g)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO games (id, title, platform, path, game_code, cover, background, icon, emulator, active_preset,
                last_played, playtime, favorite, controller_profile, graphics_profile, save_path, special, added_at,
                file_size, placeholder, missing, custom_title, hidden, lang_data)
            VALUES ($id, $title, $platform, $path, $code, $cover, $bg, $icon, $emu, $preset, $last, $play, $fav,
                $ctrl, $gfx, $save, $special, $added, $size, $ph, 0, $custom, $hidden, $lang)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title, platform = excluded.platform, path = excluded.path, game_code = excluded.game_code,
                cover = excluded.cover, background = excluded.background, icon = excluded.icon, emulator = excluded.emulator,
                active_preset = excluded.active_preset, last_played = excluded.last_played, playtime = excluded.playtime,
                favorite = excluded.favorite, controller_profile = excluded.controller_profile,
                graphics_profile = excluded.graphics_profile, save_path = excluded.save_path, special = excluded.special,
                file_size = excluded.file_size, placeholder = excluded.placeholder, missing = 0,
                custom_title = excluded.custom_title, hidden = excluded.hidden, lang_data = excluded.lang_data;
            """;
        cmd.Parameters.AddWithValue("$id", g.Id);
        cmd.Parameters.AddWithValue("$title", g.Title);
        cmd.Parameters.AddWithValue("$platform", (int)g.Platform);
        cmd.Parameters.AddWithValue("$path", g.Path);
        cmd.Parameters.AddWithValue("$code", (object?)g.GameCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cover", (object?)g.CoverPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$bg", (object?)g.BackgroundPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$icon", (object?)g.IconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$emu", g.EmulatorId);
        cmd.Parameters.AddWithValue("$preset", (object?)g.ActivePresetId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$last", (object?)g.LastPlayed?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$play", g.PlayTimeSeconds);
        cmd.Parameters.AddWithValue("$fav", g.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$ctrl", (object?)g.ControllerProfile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$gfx", (object?)g.GraphicsProfile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$save", (object?)g.SavePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$special", (int)g.Special);
        cmd.Parameters.AddWithValue("$added", g.AddedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$size", g.FileSize);
        cmd.Parameters.AddWithValue("$ph", g.IsPlaceholder ? 1 : 0);
        cmd.Parameters.AddWithValue("$custom", g.CustomTitle ? 1 : 0);
        cmd.Parameters.AddWithValue("$hidden", g.HiddenOnHome ? 1 : 0);
        cmd.Parameters.AddWithValue("$lang", System.Text.Json.JsonSerializer.Serialize(g.Languages));
        cmd.ExecuteNonQuery();
    }

    private static GameLanguageData ParseLanguages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new GameLanguageData();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<GameLanguageData>(json) ?? new GameLanguageData();
        }
        catch (System.Text.Json.JsonException)
        {
            return new GameLanguageData();
        }
    }

    /// <summary>Markiert Spiele als fehlend, die beim Scan nicht mehr gefunden wurden (Spielzeit bleibt erhalten).</summary>
    public void MarkMissing(IEnumerable<string> ids)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var id in ids)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE games SET missing = 1 WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Delete(string id)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM games WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void AddSession(PlaySession s)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        using (var cmd = c.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO sessions (game_id, preset, emulator, started, duration, exit_code, profile_id)
                VALUES ($g, $p, $e, $s, $d, $x, $prof);
                UPDATE games SET playtime = playtime + $secs, last_played = $s WHERE id = $g;
                """;
            cmd.Parameters.AddWithValue("$g", s.GameId);
            cmd.Parameters.AddWithValue("$p", (object?)s.Preset ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$e", s.Emulator);
            cmd.Parameters.AddWithValue("$s", s.Started.ToString("o"));
            cmd.Parameters.AddWithValue("$d", s.DurationSeconds);
            cmd.Parameters.AddWithValue("$x", (object?)s.ExitCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$prof", (object?)s.ProfileId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$secs", (long)Math.Round(s.DurationSeconds));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public List<PlaySession> Sessions(string? gameId = null, int limit = 200)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT game_id, preset, emulator, started, duration, exit_code, profile_id FROM sessions"
                          + (gameId != null ? " WHERE game_id = $g" : "") + " ORDER BY id DESC LIMIT $l";
        if (gameId != null)
            cmd.Parameters.AddWithValue("$g", gameId);
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        var list = new List<PlaySession>();
        while (r.Read())
        {
            list.Add(new PlaySession(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2),
                ParseDate(r.GetString(3)) ?? DateTimeOffset.MinValue, r.GetDouble(4), r.IsDBNull(5) ? null : r.GetInt32(5),
                r.IsDBNull(6) ? null : r.GetString(6)));
        }
        return list;
    }

    public void AddLaunch(LaunchAttempt a)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO launches (game_id, profile_id, started, success) VALUES ($g, $p, $s, $ok)";
        cmd.Parameters.AddWithValue("$g", a.GameId);
        cmd.Parameters.AddWithValue("$p", (object?)a.ProfileId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$s", a.Started.ToString("o"));
        cmd.Parameters.AddWithValue("$ok", a.Success ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public List<LaunchAttempt> Launches()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT game_id, profile_id, started, success FROM launches ORDER BY id";
        using var r = cmd.ExecuteReader();
        var list = new List<LaunchAttempt>();
        while (r.Read())
            list.Add(new LaunchAttempt(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1),
                ParseDate(r.GetString(2)) ?? DateTimeOffset.MinValue, r.GetInt32(3) != 0));
        return list;
    }

    public HashSet<string> Favorites(string profileId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT game_id FROM favorites WHERE profile_id = $p";
        cmd.Parameters.AddWithValue("$p", profileId);
        using var r = cmd.ExecuteReader();
        var set = new HashSet<string>();
        while (r.Read())
            set.Add(r.GetString(0));
        return set;
    }

    public void SetFavorite(string profileId, string gameId, bool favorite)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = favorite
            ? "INSERT OR IGNORE INTO favorites (profile_id, game_id) VALUES ($p, $g)"
            : "DELETE FROM favorites WHERE profile_id = $p AND game_id = $g";
        cmd.Parameters.AddWithValue("$p", profileId);
        cmd.Parameters.AddWithValue("$g", gameId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Übernimmt Daten aus der Zeit vor den Profilen einmalig in das erste aktive Profil:
    /// Favoriten sowie Sessions/Starts ohne Profil.
    /// </summary>
    public void MigrateLegacyFavorites(string profileId)
    {
        if (GetSetting("sessions_migrated") == null)
        {
            using (var c = Open())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = """
                    UPDATE sessions SET profile_id = $p WHERE profile_id IS NULL;
                    UPDATE launches SET profile_id = $p WHERE profile_id IS NULL;
                    """;
                cmd.Parameters.AddWithValue("$p", profileId);
                cmd.ExecuteNonQuery();
            }
            SetSetting("sessions_migrated", profileId);
        }
        if (GetSetting("favorites_migrated") != null)
            return;
        using (var c = Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "INSERT OR IGNORE INTO favorites (profile_id, game_id) SELECT $p, id FROM games WHERE favorite = 1";
            cmd.Parameters.AddWithValue("$p", profileId);
            cmd.ExecuteNonQuery();
        }
        SetSetting("favorites_migrated", profileId);
    }

    public string? GetSetting(string key)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string? value)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static DateTimeOffset? ParseDate(string? s) =>
        s != null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;
}
