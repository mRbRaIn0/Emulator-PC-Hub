using EmulatorPCHub.Core.Config;

namespace EmulatorPCHub.Core;

/// <summary>
/// Erst- und Zweitsprache des Hubs (Einstellungen). Bestimmt die Oberflächensprache (Deutsch/Englisch)
/// und welche Titel/Cover-Variante eines Spiels angezeigt wird.
/// </summary>
public static class HubLanguage
{
    /// <summary>Sprachcode → Anzeigename (in der Sprache selbst). Die Oberfläche selbst gibt es in <see cref="UiLanguages"/>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["de"] = "Deutsch",
        ["en"] = "English",
        ["fr"] = "Français",
        ["es"] = "Español",
        ["it"] = "Italiano",
        ["nl"] = "Nederlands",
        ["pt"] = "Português",
        ["ru"] = "Русский",
        ["ja"] = "日本語",
        ["ko"] = "한국어",
        ["zh"] = "中文",
    };

    /// <summary>Sprachen, in denen die Hub-Oberfläche übersetzt ist.</summary>
    public static readonly string[] UiLanguages = ["de", "en"];

    public static string Primary { get; private set; } = "de";
    public static string Secondary { get; private set; } = "en";

    /// <summary>Aktive Oberflächensprache: Erstsprache, falls übersetzt – sonst Zweitsprache, sonst Deutsch.</summary>
    public static string Ui =>
        UiLanguages.Contains(Primary) ? Primary : UiLanguages.Contains(Secondary) ? Secondary : "de";

    public static bool IsEnglish => Ui == "en";

    public static event EventHandler? Changed;

    public static void Apply(UiConfig ui)
    {
        var primary = Normalize(ui.PrimaryLanguage, "de");
        var secondary = Normalize(ui.SecondaryLanguage, "en");
        if (primary == Primary && secondary == Secondary)
            return;
        Primary = primary;
        Secondary = secondary;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string Name(string code) => Names.TryGetValue(code, out var n) ? n : code;

    /// <summary>Bevorzugte Sprachen in Reihenfolge (Erst-, dann Zweitsprache).</summary>
    public static IEnumerable<string> Preferred()
    {
        yield return Primary;
        if (Secondary != Primary)
            yield return Secondary;
    }

    private static string Normalize(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && Names.ContainsKey(code.Trim().ToLowerInvariant()) ? code.Trim().ToLowerInvariant() : fallback;
}
