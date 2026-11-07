using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels;

/// <summary>
/// Tomorrow's suggestion card (FR-6): a skeleton while loading, then live data or the generic "Saran umum" state.
/// Never shows an error: a missing answer after <see cref="Timeout"/> falls back to the generic suggestion (AC-4.2).
/// </summary>
public sealed partial class WeatherSuggestionViewModel : ObservableObject
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    private readonly IWeatherSuggestionService _service;
    private readonly ISessionService _session;
    private readonly IClock _clock;
    private readonly IUiText _text;
    private readonly TimeSpan _timeout;
    private CancellationTokenSource? _loading;

    public WeatherSuggestionViewModel(
        IWeatherSuggestionService service, ISessionService session, IClock clock, IUiText text, TimeSpan? timeout = null)
    {
        _service = service;
        _session = session;
        _clock = clock;
        _text = text;
        _timeout = timeout ?? Timeout;
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isFallback = true;
    [ObservableProperty] private string _cityLine = string.Empty;
    [ObservableProperty] private string _contentTitle = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasContentSubtitle))] private string? _contentSubtitle;
    [ObservableProperty] private string _adviceText = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImpact))] private string? _impactText;
    [ObservableProperty] private string _footerText = string.Empty;

    public string AdviceHeading => _text.Get("Weather.AdviceHeading");

    public string LoadingText => _text.Get("Weather.Loading");

    public bool HasContentSubtitle => !string.IsNullOrEmpty(ContentSubtitle);

    public bool HasImpact => !string.IsNullOrEmpty(ImpactText);

    /// <summary>Completes when the current load ended (data, fallback or cancelled).</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public void StartLoading()
    {
        Cancel();
        var loading = _loading = new CancellationTokenSource(_timeout);
        IsLoading = true;
        LoadTask = LoadAsync(loading);
    }

    /// <summary>Stops a load in flight and drops its result; called when the screen is left.</summary>
    public void Cancel()
    {
        var loading = _loading;
        _loading = null;
        loading?.Cancel();
        loading?.Dispose();
        IsLoading = false;
    }

    private async Task LoadAsync(CancellationTokenSource loading)
    {
        WeatherSuggestionDto suggestion;
        try
        {
            suggestion = await _service.GetTomorrowSuggestionAsync(loading.Token).WaitAsync(loading.Token);
        }
        catch (OperationCanceledException) when (!ReferenceEquals(_loading, loading))
        {
            return;
        }
        catch (OperationCanceledException)
        {
            suggestion = FallbackSuggestion();
        }

        if (!ReferenceEquals(_loading, loading))
        {
            return;
        }

        _loading = null;
        loading.Dispose();
        Show(suggestion);
        IsLoading = false;
    }

    private WeatherSuggestionDto FallbackSuggestion() =>
        new(_session.Current?.City ?? string.Empty, _clock.Today.AddDays(1), IsFallback: true, ForecastSummary: null, AccuWeatherIconCode: null,
            _text.Get("Weather.FallbackAdvice"), SuggestedActivityTypeId: null, EstimatedCo2Kg: null);

    private void Show(WeatherSuggestionDto suggestion)
    {
        IsFallback = suggestion.IsFallback;
        CityLine = string.Format(CultureInfo.CurrentCulture, _text.Get("Weather.CityLine"), suggestion.City);
        ContentTitle = suggestion.IsFallback
            ? _text.Get("Weather.FallbackTitle")
            : string.Format(CultureInfo.CurrentCulture, _text.Get("Weather.TomorrowIn"), suggestion.City);
        ContentSubtitle = suggestion.IsFallback ? null : suggestion.ForecastSummary;
        AdviceText = suggestion.SuggestionText;
        ImpactText = suggestion.EstimatedCo2Kg is { } kg
            ? string.Format(CultureInfo.CurrentCulture, _text.Get("Weather.Impact"), UiFormat.Co2(kg))
            : null;
        FooterText = suggestion.IsFallback
            ? _text.Get("Weather.FallbackFooter")
            : string.Format(CultureInfo.CurrentCulture, _text.Get("Weather.Disclaimer.Live"), suggestion.City);
    }
}
