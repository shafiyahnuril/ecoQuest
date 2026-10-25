using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EcoQuest.ViewModels;

namespace EcoQuest.Controls;

/// <summary>
/// Picks one of four views from <see cref="ViewModelBase.State"/> / <see cref="ViewModelBase.IsEmpty"/> (UIR-05), so every data screen
/// behaves the same without logic in the View. Bind State, IsEmpty, ErrorMessage and RetryCommand to the ViewModel, then fill the slots:
/// <list type="bullet">
/// <item>Idle or Loading: <see cref="LoadingContent"/> (Idle counts as Loading so there is no empty flash before the first load).</item>
/// <item>Error: an <see cref="ErrorBanner"/> with <see cref="ErrorMessage"/> and the retry command (the retry text is Common.Retry).</item>
/// <item>Loaded and IsEmpty: <see cref="EmptyContent"/>.</item>
/// <item>Loaded: <see cref="Content"/>.</item>
/// </list>
/// The template and its triggers live in Resources/Theme/Controls.xaml.
/// </summary>
public class StateHost : Control
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(LoadState), typeof(StateHost), new PropertyMetadata(LoadState.Idle));

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(nameof(IsEmpty), typeof(bool), typeof(StateHost), new PropertyMetadata(false));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(StateHost));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(StateHost));

    public static readonly DependencyProperty LoadingContentProperty =
        DependencyProperty.Register(nameof(LoadingContent), typeof(object), typeof(StateHost));

    public static readonly DependencyProperty EmptyContentProperty =
        DependencyProperty.Register(nameof(EmptyContent), typeof(object), typeof(StateHost));

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(StateHost));

    static StateHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StateHost), new FrameworkPropertyMetadata(typeof(StateHost)));
        IsTabStopProperty.OverrideMetadata(typeof(StateHost), new FrameworkPropertyMetadata(false));
        FocusableProperty.OverrideMetadata(typeof(StateHost), new FrameworkPropertyMetadata(false));
    }

    public LoadState State { get => (LoadState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public bool IsEmpty { get => (bool)GetValue(IsEmptyProperty); set => SetValue(IsEmptyProperty, value); }

    public string? ErrorMessage { get => (string?)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    public ICommand? RetryCommand { get => (ICommand?)GetValue(RetryCommandProperty); set => SetValue(RetryCommandProperty, value); }

    public object? LoadingContent { get => GetValue(LoadingContentProperty); set => SetValue(LoadingContentProperty, value); }

    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
}
