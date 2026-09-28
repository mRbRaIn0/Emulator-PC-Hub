using System.Reflection;

namespace EmulatorPCHub.Core;

/// <summary>Version des Hubs (aus Directory.Build.props → &lt;Version&gt;).</summary>
public static class HubInfo
{
    public static Version Version { get; } = typeof(HubInfo).Assembly.GetName().Version ?? new Version(1, 0);

    /// <summary>Anzeige-Version, z. B. „1.0“ (Patch-Stelle nur, wenn sie nicht 0 ist).</summary>
    public static string DisplayVersion => Version.Build > 0 ? $"{Version.Major}.{Version.Minor}.{Version.Build}" : $"{Version.Major}.{Version.Minor}";
}
