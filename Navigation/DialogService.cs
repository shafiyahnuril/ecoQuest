using System.Windows;
using EcoQuest.ViewModels;
using EcoQuest.Views.Dialogs;

namespace EcoQuest.Navigation;

public sealed class DialogService(IUiText text, ToastViewModel toast) : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null)
    {
        var viewModel = new ConfirmDialogViewModel(title, message, confirmText, cancelText ?? text.Get("Common.Cancel"));
        return Task.FromResult(ShowModal(viewModel));
    }

    public Task ShowInfoAsync(string title, string message, string? buttonText = null)
    {
        ShowModal(new ConfirmDialogViewModel(title, message, buttonText ?? text.Get("Common.Continue"), cancelText: null));
        return Task.CompletedTask;
    }

    public void ShowToast(string message) => toast.Show(message);

    public void ShowThemePreview()
    {
#if DEBUG
        // Separate window so the preview never mixes with the real shell; reachable via Ctrl+Shift+T (MainWindow).
        var window = new Window
        {
            Title = "Pratinjau tema (DEBUG)",
            Width = 1100,
            Height = 800,
            Style = (Style)Application.Current.FindResource("Window.Base"),
            Content = new ecoQuest.Views.Dev.ThemePreviewView(),
        };
        window.Show();
#endif
    }

    // The dialog window is a borderless, transparent overlay that covers the owner's client area, so its
    // Overlay.Backdrop dims the shell like .modal-backdrop in the prototype.
    private static bool ShowModal(ConfirmDialogViewModel viewModel)
    {
        var dialog = new ConfirmDialog { DataContext = viewModel };

        if (Application.Current.MainWindow is { IsLoaded: true, Content: FrameworkElement client } owner
            && PresentationSource.FromVisual(client) is { CompositionTarget: not null } source)
        {
            var origin = source.CompositionTarget.TransformFromDevice.Transform(client.PointToScreen(new Point(0, 0)));
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            dialog.Left = origin.X;
            dialog.Top = origin.Y;
            dialog.Width = client.ActualWidth;
            dialog.Height = client.ActualHeight;
        }

        return dialog.ShowDialog() == true;
    }
}
