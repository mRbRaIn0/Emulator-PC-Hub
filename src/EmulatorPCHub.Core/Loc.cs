using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace EmulatorPCHub.Core;

/// <summary>
/// Übersetzung der Oberfläche. Die Quelltexte sind deutsch; bei englischer Oberflächensprache
/// (<see cref="HubLanguage.IsEnglish"/>) wird der Text über <see cref="T"/> ersetzt. Unbekannte Texte bleiben unverändert.
/// </summary>
public static partial class Loc
{
    private sealed record Pattern(Regex Regex, string English);

    private static readonly ConcurrentDictionary<string, string> Cache = new();
    private static readonly Lazy<Dictionary<string, string>> Exact = new(() => new Dictionary<string, string>(Entries.Plain, StringComparer.Ordinal));
    private static readonly Lazy<Pattern[]> Patterns = new(() => Entries.Templates.OrderByDescending(t => PlaceholderRegex().Replace(t.de, "").Length).Select(t => new Pattern(ToRegex(t.de), t.en)).ToArray());
    private static readonly Lazy<KeyValuePair<string, string>[]> Fragments = new(() =>
        Exact.Value.Where(kv => kv.Key.Trim().Length >= 14).OrderByDescending(kv => kv.Key.Length).ToArray());

    /// <summary>Übersetzt <paramref name="text"/> in die aktuelle Oberflächensprache.</summary>
    public static string T(string? text)
    {
        if (string.IsNullOrEmpty(text) || !HubLanguage.IsEnglish)
            return text ?? "";
        return Cache.GetOrAdd(text, Translate);
    }

    private static string Translate(string text)
    {
        if (Exact.Value.TryGetValue(text, out var direct))
            return direct;
        var trimmed = text.Trim();
        if (trimmed.Length != text.Length && Exact.Value.TryGetValue(trimmed, out var t2))
            return text[..(text.Length - text.TrimStart().Length)] + t2 + text[text.TrimEnd().Length..];

        foreach (var p in Patterns.Value)
        {
            var m = p.Regex.Match(text);
            if (!m.Success)
                continue;
            var result = p.English;
            for (var i = m.Groups.Count - 1; i >= 1; i--)
                result = result.Replace("{" + (i - 1) + "}", T(m.Groups[i].Value));
            return result;
        }

        // Zusammengesetzte Texte („…" + „…") und Texte mit mehreren bekannten Satzteilen
        var changed = text;
        foreach (var kv in Fragments.Value)
            if (changed.Contains(kv.Key, StringComparison.Ordinal))
                changed = changed.Replace(kv.Key, kv.Value, StringComparison.Ordinal);
        return changed;
    }

    private static Regex ToRegex(string template)
    {
        var sb = new System.Text.StringBuilder("^");
        var i = 0;
        while (i < template.Length)
        {
            var m = PlaceholderRegex().Match(template, i);
            if (m.Success && m.Index == i)
            {
                sb.Append("(.+?)");
                i += m.Length;
                continue;
            }
            sb.Append(Regex.Escape(template[i].ToString()));
            i++;
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();
}
