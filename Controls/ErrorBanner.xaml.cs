using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EcoQuest.Controls;

/// <summary>Error banner with a retry button. Message and RetryText are supplied by the caller (no texts of its own).</summary>
public partial class ErrorBanner : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(ErrorBanner));

    public static readonly DependencyProperty RetryTextProperty =
        DependencyProperty.Register(nameof(RetryText), typeof(string), typeof(ErrorBanner));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(ErrorBanner));

    public ErrorBanner()
    {
        InitializeComponent();
    }

    public string? Message { get => (string?)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public string? RetryText { get => (string?)GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    public ICommand? RetryCommand { get => (ICommand?)GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }
}
