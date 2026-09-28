using CommunityToolkit.Mvvm.ComponentModel;

namespace EmulatorPCHub.UI.ViewModels;

/// <summary>Auswahl-Eintrag (Edition, Engine …) mit Status für die Spezialseiten.</summary>
public sealed partial class OptionItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; init; } = "";
    public IReadOnlyList<string> Features { get; init; } = [];
    public string FeaturesText => string.Join(" · ", Features);

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private string _status = "";

    public string Radio => IsSelected ? "●" : "○";

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(Radio));
}
