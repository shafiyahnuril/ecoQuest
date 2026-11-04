using CommunityToolkit.Mvvm.ComponentModel;

namespace EcoQuest.ViewModels;

/// <summary>One sidebar entry. Keys are resolved to copy/geometry by the View (Strings.id.xaml, Icons.xaml).</summary>
public sealed partial class NavItemViewModel(MainMenu menu, string labelKey, string iconKey) : ObservableObject
{
    public MainMenu Menu { get; } = menu;

    public string LabelKey { get; } = labelKey;

    public string IconKey { get; } = iconKey;

    [ObservableProperty] private bool _isSelected;

    /// <summary>Amber dot ("belum refleksi"); only the Evening Review entry ever turns it on.</summary>
    [ObservableProperty] private bool _isPendingIndicatorVisible;
}
