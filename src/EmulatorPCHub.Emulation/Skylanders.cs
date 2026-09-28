using EmulatorPCHub.Core.Models;

namespace EmulatorPCHub.Emulation;

/// <summary>Skylanders-Spiele brauchen das emulierte „Portal of Power“ (USB) samt Fenster zum Figurenwechsel.</summary>
public static class Skylanders
{
    public static bool IsSkylandersGame(GameEntry game) => game.Title.Contains("Skylanders", StringComparison.OrdinalIgnoreCase);
}
