using System.Text;

namespace EmulatorPCHub.Emulation;

/// <summary>Fehler, der dem Benutzer direkt angezeigt werden kann (z. B. fehlender Emulator).</summary>
public sealed class LaunchException : Exception
{
    public LaunchException(string message) : base(message) { }
}

public static class CommandLine
{
    /// <summary>Zerlegt eine Argumentzeile (mit Anführungszeichen) in einzelne Argumente.</summary>
    public static List<string> Split(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }
}
