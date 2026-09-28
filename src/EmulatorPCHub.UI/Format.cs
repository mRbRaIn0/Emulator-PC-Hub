using System.Globalization;

namespace EmulatorPCHub.UI;

public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static string PlayTime(long seconds)
    {
        if (seconds < 60)
            return seconds == 0 ? "Noch nicht gespielt" : "Weniger als 1 Minute";
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours < 1)
            return $"{(int)t.TotalMinutes} Min.";
        return $"{(int)t.TotalHours} Std. {t.Minutes} Min.";
    }

    public static string LastPlayed(DateTimeOffset? when)
    {
        if (when == null)
            return "";
        var days = (DateTime.Today - when.Value.LocalDateTime.Date).Days;
        return days switch
        {
            0 => "Heute gespielt",
            1 => "Gestern gespielt",
            < 7 => $"Vor {days} Tagen gespielt",
            _ => $"Zuletzt {when.Value.LocalDateTime.ToString("d. MMMM yyyy", De)}",
        };
    }

    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };

    public static string Clock(DateTime now, bool h24) => h24 ? now.ToString("HH:mm", De) : now.ToString("h:mm tt", CultureInfo.InvariantCulture);
}
