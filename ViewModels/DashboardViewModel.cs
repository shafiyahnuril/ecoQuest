using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Controls.Charts;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace EcoQuest.ViewModels;

/// <summary>
/// Dashboard / Beranda (S-04, SDD-Frontend 3.4). Text only: all numbers come from <see cref="IDashboardService"/> and are formatted
/// with <see cref="UiFormat"/>; chart series come from <see cref="ChartFactory"/>. Insights and weather are filled by later tasks.
/// The view model is transient, so every navigation to the screen starts with the skeleton; "silent" only applies to
/// <see cref="ReloadCommand"/> on the same instance (UIR-56), where the old content stays visible while the new data loads.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    /// <summary>Shown in the "Hari refleksi" card when the profile call failed (the dashboard itself still loads).</summary>
    public const string UnknownValue = "—";

    private static readonly CultureInfo PercentCulture = CultureInfo.GetCultureInfo("id-ID");

    private readonly IDashboardService _dashboard;
    private readonly IProfileService _profile;
    private readonly ISessionService _session;
    private readonly IClock _clock;
    private readonly INavigationService _navigation;
    private readonly IUiText _text;
    private CancellationTokenSource? _cts;
    private DateOnly? _loadedFor;

    public DashboardViewModel(
        IDashboardService dashboard, IProfileService profile, ISessionService session,
        IClock clock, INavigationService navigation, IUiText text)
    {
        _dashboard = dashboard;
        _profile = profile;
        _session = session;
        _clock = clock;
        _navigation = navigation;
        _text = text;
        TodayText = UiFormat.LongDate(_clock.Today);
        Greeting = BuildGreeting();
        BannerText = _text.Get("Dashboard.PendingBanner");
        HeroCtaText = _text.Get("Dashboard.HeroCta.Fill");
        TotalCo2Text = UiFormat.Kg(0m);
        ReflectionDaysText = UnknownValue;
        StreakText = UiFormat.Count(0);
        LevelName = "";
    }

    /// <summary>"Selamat malam, {nama}!"</summary>
    [ObservableProperty] private string _greeting;

    /// <summary>"Sabtu, 24 Oktober 2026" (from <see cref="IClock"/>).</summary>
    [ObservableProperty] private string _todayText;

    /// <summary>True when today's review is saved: the banner turns into the confirmation and the hero button reads "Lihat jurnal hari ini".</summary>
    [ObservableProperty] private bool _isSubmitted;

    [ObservableProperty] private string _bannerText;
    [ObservableProperty] private string _heroCtaText;

    /// <summary>Value of the first metric card, e.g. "9,45 kg" (the card label already says CO₂).</summary>
    [ObservableProperty] private string _totalCo2Text;

    /// <summary>Tooltip of the first metric card; written by the service (FR-4.5), never by the UI.</summary>
    [ObservableProperty] private string _totalCo2Explanation = "";

    [ObservableProperty] private string _reflectionDaysText;
    [ObservableProperty] private string _streakText;
    [ObservableProperty] private string _levelName;

    [ObservableProperty] private IReadOnlyList<CategoryLegendItem> _breakdownLegend = [];
    [ObservableProperty] private ISeries[] _breakdownSeries = [];
    [ObservableProperty] private bool _hasBreakdownData;
    [ObservableProperty] private string _breakdownAutomationText = "";

    /// <summary>Range of the trend line only; the donut always shows the cumulative breakdown.</summary>
    [ObservableProperty] private TrendRange _selectedRange = TrendRange.Weekly;

    [ObservableProperty] private ISeries[] _trendSeries = [];
    [ObservableProperty] private Axis[] _trendXAxes = [];
    [ObservableProperty] private Axis[] _trendYAxes = [];
    [ObservableProperty] private bool _hasTrendData;
    [ObservableProperty] private string _trendAutomationText = "";

    /// <summary>Set when reloading the trend failed; the rest of the dashboard stays visible.</summary>
    [ObservableProperty] private string? _trendErrorMessage;

    /// <summary>"{kota} · Saran berdasarkan kota domisili": heading line of the weather card, from the signed-in user's city.</summary>
    // TODO(FE insight/weather task): replaced by the city of the real WeatherSuggestionDto.
    [ObservableProperty] private string _cityLineText = "";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        await LoadAsync(silent: false, _cts.Token);
    }

    public override void OnNavigatedFrom()
    {
        _cts?.Cancel();
    }

    /// <summary>Reloads in place. After a successful load the old content stays visible (silent); from the Error state it shows the skeleton again, so it also serves as "Coba lagi".</summary>
    [RelayCommand]
    private Task ReloadAsync() => LoadAsync(silent: true, (_cts ??= new CancellationTokenSource()).Token);

    /// <summary>Window re-activated (UIR-56): reload only when the Jakarta date changed since the last load.</summary>
    [RelayCommand]
    private async Task RefreshIfDateChangedAsync()
    {
        if (State == LoadState.Loaded && _loadedFor is { } loaded && loaded != _clock.Today)
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task SetTrendRangeAsync(TrendRange range)
    {
        if (range == SelectedRange || State != LoadState.Loaded)
        {
            return;
        }

        var data = await RunAsync(token => _dashboard.GetDashboardAsync(range, token), (_cts ??= new CancellationTokenSource()).Token, silent: true);
        if (data is not null)
        {
            SelectedRange = range;
            TrendErrorMessage = null;
            ApplyTrend(data.Trend, range);
            return;
        }

        if (State == LoadState.Error)
        {
            TrendErrorMessage = ErrorMessage;
            ErrorMessage = null;
            State = LoadState.Loaded;
            OnPropertyChanged(nameof(SelectedRange));   // puts the chips back on the range that is still drawn
        }
    }

    [RelayCommand]
    private Task StartReviewAsync() => _navigation.NavigateTo<EveningReviewViewModel>();

    [RelayCommand]
    private Task ViewSummaryAsync() => _navigation.NavigateTo<EveningReviewViewModel>();

    private async Task LoadAsync(bool silent, CancellationToken ct)
    {
        var today = _clock.Today;
        Result<UserProfileDto>? profile = null;

        var data = await RunAsync(async token =>
        {
            // Both calls start together; a failing profile call must not fail the dashboard.
            var dashboardTask = _dashboard.GetDashboardAsync(SelectedRange, token);
            var profileTask = _profile.GetProfileAsync(token);
            var result = await dashboardTask;
            if (result.IsSuccess)
            {
                profile = await profileTask;   // a dashboard error is shown at once, without waiting for the profile call
            }

            return result;
        }, ct, silent);

        if (data is null)
        {
            return;
        }

        _loadedFor = today;
        Apply(data, profile, today);
    }

    private void Apply(DashboardDto data, Result<UserProfileDto>? profile, DateOnly today)
    {
        Greeting = BuildGreeting();
        CityLineText = string.Format(CultureInfo.CurrentCulture, _text.Get("Weather.CityLine"), _session.Current?.City ?? "");
        TodayText = UiFormat.LongDate(today);
        IsSubmitted = data.TodayState == TodayReviewState.Submitted;
        HeroCtaText = _text.Get(IsSubmitted ? "Dashboard.HeroCta.View" : "Dashboard.HeroCta.Fill");
        BannerText = IsSubmitted ? BuildSavedBanner(data, today) : _text.Get("Dashboard.PendingBanner");

        TotalCo2Text = UiFormat.Kg(data.TotalCo2Kg);
        TotalCo2Explanation = data.TotalCo2Explanation;
        StreakText = UiFormat.Count(data.CurrentStreak);
        LevelName = data.Level.LevelName;
        TrendErrorMessage = null;
        SelectedRange = data.Range;
        ApplyTrend(data.Trend, data.Range);
        ApplyBreakdown(data.Breakdown);
        ReflectionDaysText = profile is { IsSuccess: true, Value: not null }
            ? UiFormat.Count(profile.Value.TotalReviewCount)
            : UnknownValue;

        // An account without reviews is a normal, loaded screen with zeros (UIR-57), never a separate empty screen.
        IsEmpty = false;
    }

    private void ApplyTrend(IReadOnlyList<TrendPointDto> trend, TrendRange range)
    {
        HasTrendData = trend.Any(p => p.HasReview);
        TrendSeries = ChartFactory.Trend(trend);
        TrendXAxes = ChartFactory.TrendXAxes(trend, range);
        TrendYAxes = ChartFactory.TrendYAxes(trend);
        TrendAutomationText = _text.Get("Dashboard.TrendTitle") + ": " + string.Join("; ", trend.Select(DescribeTrendPoint));
    }

    private string DescribeTrendPoint(TrendPointDto point) =>
        UiFormat.Date(point.Date) + " " + (point.HasReview ? UiFormat.Co2(point.Co2Kg) : _text.Get("Dashboard.TrendNoReview"));

    private void ApplyBreakdown(IReadOnlyList<CategoryBreakdownDto> breakdown)
    {
        HasBreakdownData = breakdown.Any(b => b.Co2Kg > 0);
        BreakdownLegend = breakdown
            .Select(b => new CategoryLegendItem(b.CategoryId, b.CategoryName, HasBreakdownData ? FormatPercent(b.Percentage) : ""))
            .ToList();
        BreakdownSeries = HasBreakdownData ? ChartFactory.Breakdown(breakdown) : [];
        BreakdownAutomationText = _text.Get(HasBreakdownData ? "Dashboard.BreakdownTitle" : "Dashboard.BreakdownAutomation")
            + (HasBreakdownData ? ": " + string.Join("; ", BreakdownLegend.Select(l => $"{l.Name} {l.PercentText}")) : "");
    }

    private static string FormatPercent(decimal percentage) => percentage.ToString("0.0", PercentCulture) + "%";

    private string BuildGreeting() =>
        string.Format(CultureInfo.CurrentCulture, _text.Get("Dashboard.Greeting"), _session.Current?.UserName ?? "");

    /// <summary>"Refleksi hari ini tersimpan. 0,90 kg CO₂"; without today's trend point only the sentence is shown.</summary>
    private string BuildSavedBanner(DashboardDto data, DateOnly today)
    {
        var point = data.Trend.FirstOrDefault(p => p.Date == today && p.HasReview);
        if (point is null)
        {
            return _text.Get("Review.Saved") + ".";
        }

        return string.Format(CultureInfo.CurrentCulture, _text.Get("Dashboard.SavedBanner"), UiFormat.Co2(point.Co2Kg)).TrimEnd();
    }
}

/// <summary>One legend row: colour comes from <see cref="CategoryId"/> (CategoryIdToBrushConverter), always next to the name (NFR-12); <see cref="PercentText"/> is empty while there is no data.</summary>
public sealed record CategoryLegendItem(int CategoryId, string Name, string PercentText);
