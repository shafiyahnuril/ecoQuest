using System.Windows;
using System.Windows.Controls;
using EcoQuest.ViewModels;

namespace EcoQuest.Views;

public partial class DashboardView : UserControl
{
    private Window? _window;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // Trigger only (UIR-56): the view model decides whether the date changed and reloads silently.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OnUnloaded(sender, e);
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.Activated += OnWindowActivated;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window = null;
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e) =>
        (DataContext as DashboardViewModel)?.RefreshIfDateChangedCommand.Execute(null);
}
