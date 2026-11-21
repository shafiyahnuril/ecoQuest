using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>One dot of the step indicator.</summary>
public sealed partial class OnboardingDot : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;
}

/// <summary>
/// Onboarding S-03 (UIR-72): three steps shown once after the first registration. "Lewati" and the last step's "Mulai" both remember
/// the user as done and continue to the Dashboard. Texts come from Strings.id.xaml (Onboarding.N.Title / Body).
/// </summary>
public sealed partial class OnboardingViewModel : ViewModelBase
{
    public const int StepCount = 3;

    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IOnboardingStore _store;
    private readonly IUiText _text;

    public OnboardingViewModel(INavigationService navigation, ISessionService session, IOnboardingStore store, IUiText text)
    {
        _navigation = navigation;
        _session = session;
        _store = store;
        _text = text;
        Dots = [.. Enumerable.Range(0, StepCount).Select(i => new OnboardingDot { IsActive = i == 0 })];
    }

    public IReadOnlyList<OnboardingDot> Dots { get; }

    /// <summary>Bodies of all steps; the view stacks them so the card keeps one height.</summary>
    public IReadOnlyList<string> StepBodies => [.. Enumerable.Range(1, StepCount).Select(n => _text.Get($"Onboarding.{n}.Body"))];

    /// <summary>Zero-based; drives the illustration of the view.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle), nameof(StepBody), nameof(StepIndicator), nameof(StepIndicatorLabel), nameof(IsLastStep), nameof(NextLabel))]
    private int _stepIndex;

    public string StepTitle => _text.Get($"Onboarding.{StepIndex + 1}.Title");

    public string StepBody => _text.Get($"Onboarding.{StepIndex + 1}.Body");

    /// <summary>"1/3". Not drawn (prototype v7 shows dots); used for the screen-reader label.</summary>
    public string StepIndicator => $"{StepIndex + 1}/{StepCount}";

    public string StepIndicatorLabel => string.Format(_text.Get("Onboarding.StepLabel"), StepIndex + 1, StepCount);

    public bool IsLastStep => StepIndex == StepCount - 1;

    public string NextLabel => _text.Get(IsLastStep ? "Onboarding.Start" : "Onboarding.Next");

    partial void OnStepIndexChanged(int value)
    {
        for (var i = 0; i < Dots.Count; i++)
        {
            Dots[i].IsActive = i == value;
        }
    }

    [RelayCommand]
    private Task NextAsync()
    {
        if (!IsLastStep)
        {
            StepIndex++;
            return Task.CompletedTask;
        }

        return FinishAsync();
    }

    [RelayCommand]
    private Task SkipAsync() => FinishAsync();

    private Task FinishAsync()
    {
        if (_session.Current is { } user)
        {
            _store.MarkCompleted(user.UserId);
        }

        // Same as logout in reverse: forget Register/Onboarding so nothing leads back to them.
        _navigation.ClearHistory();
        return _navigation.NavigateTo<DashboardViewModel>();
    }
}
