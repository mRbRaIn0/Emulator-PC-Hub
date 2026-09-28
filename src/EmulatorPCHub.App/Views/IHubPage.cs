using EmulatorPCHub.Controllers;

namespace EmulatorPCHub.App.Views;

/// <summary>Seiten, die Controller-Aktionen selbst behandeln und eigene Tastenhinweise anzeigen.</summary>
public interface IHubPage
{
    /// <summary>true = Aktion wurde behandelt; sonst greift die Standard-Navigation.</summary>
    bool HandleNav(NavAction action);

    /// <summary>Tastenhinweise für die Fußleiste.</summary>
    string Hints { get; }

    /// <summary>Wird aufgerufen, wenn die Seite (wieder) sichtbar wird.</summary>
    void OnShown();
}
