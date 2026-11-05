using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.ViewModels;

public enum ReviewMode { Form, Summary }

/// <summary>
/// Evening Review (S-05 form, S-06 summary). Every edit of a row goes through one debounced path that validates all rows and previews
/// the edited ones; nothing is computed or validated here (UIR-33). Saving follows UIR-35 (dialogs) and UIR-37 (results).
/// </summary>
public sealed partial class EveningReviewViewModel : ViewModelBase
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(120);

    private readonly IEveningReviewService _review;
    private readonly IReferenceDataService _reference;
    private readonly IDialogService _dialogs;
    private readonly IUiText _text;
    private readonly IClock _clock;
    private readonly INavigationService _navigation;
    private readonly ShellViewModel _shell;
    private readonly TimeSpan _debounceDelay;
    private readonly Dictionary<int, ActivityImpactDto> _impacts = [];
    private readonly HashSet<ActivityInputItemViewModel> _changed = [];
    private IReadOnlyList<ActivityInputItemViewModel> _items = [];
    private IReadOnlyList<ActivityTypeDto> _types = [];
    private ReviewValidationResult _validation = new([]) { IsEmpty = true };
    private decimal _total;
    private bool _warningsAcknowledged;
    private CancellationTokenSource? _loading;
    private CancellationTokenSource? _debounce;

    public EveningReviewViewModel(
        IEveningReviewService review, IReferenceDataService reference, IDialogService dialogs, IUiText text,
        IClock clock, INavigationService navigation, ShellViewModel shell, TimeSpan? debounce = null)
    {
        _review = review;
        _reference = reference;
        _dialogs = dialogs;
        _text = text;
        _clock = clock;
        _navigation = navigation;
        _shell = shell;
        _debounceDelay = debounce ?? DefaultDebounce;
        _todayText = UiFormat.LongDate(_clock.Today);
        _totalText = ImpactText(0m);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsForm), nameof(IsSummary), nameof(Title), nameof(Subtitle))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private ReviewMode _mode;

    [ObservableProperty] private string _todayText;
    [ObservableProperty] private IReadOnlyList<ActivityGroupViewModel> _groups = [];
    [ObservableProperty] private string _totalText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RunningTotalText))] private int _filledCount;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SubmitCommand))] private bool _hasBlockingError;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasBanner))] private string? _bannerMessage;
    [ObservableProperty] private IAsyncRelayCommand? _bannerRetryCommand;

    // TODO(FE summary task): render the full summary (S-06) from this DTO; the form only needs it to know the day is locked.
    [ObservableProperty] private ReviewSummaryDto? _summary;

    public bool IsForm => Mode == ReviewMode.Form;

    public bool IsSummary => Mode == ReviewMode.Summary;

    public bool HasBanner => BannerMessage is not null;

    public string Title => _text.Get(IsForm ? "Review.Title" : "Review.Saved");

    public string Subtitle => _text.Get(IsForm ? "Review.Subtitle" : "Review.SavedSubtitle");

    public string RunningTotalText => string.Format(CultureInfo.CurrentCulture, _text.Get("Review.RunningTotal"), FilledCount);

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        _loading?.Cancel();
        _loading = new CancellationTokenSource();
        await LoadAsync(silent: false, _loading.Token);
    }

    public override void OnNavigatedFrom()
    {
        _loading?.Cancel();
        _debounce?.Cancel();
    }

    [RelayCommand]
    private Task ReloadAsync() => LoadAsync(silent: true, LoadToken);

    [RelayCommand]
    private Task BackHomeAsync() => _navigation.NavigateTo<DashboardViewModel>();

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ClearBanner();
        Revalidate();
        if (HasBlockingError || !await ConfirmAsync())
        {
            return;
        }

        await SaveAsync();
    }

    [RelayCommand]
    private Task RetrySaveAsync()
    {
        ClearBanner();
        return SaveAsync();
    }

    private bool CanSubmit() => IsForm && !HasBlockingError;

    private CancellationToken LoadToken => (_loading ??= new CancellationTokenSource()).Token;

    private async Task LoadAsync(bool silent, CancellationToken ct)
    {
        var data = await RunAsync(LoadDataAsync, ct, silent);
        if (data is not null)
        {
            Apply(data);
        }
    }

    private async Task<Result<ReviewData>> LoadDataAsync(CancellationToken ct)
    {
        var today = await _review.GetTodayAsync(ct);
        if (today.Value is not { } dto)
        {
            return Result<ReviewData>.Failure(today.Error, today.Message ?? string.Empty);
        }

        if (dto.State == TodayReviewState.Submitted)
        {
            return Result<ReviewData>.Success(new ReviewData(dto, []));
        }

        var types = await _reference.GetActivityTypesAsync(ct);
        return types.Value is { } list
            ? Result<ReviewData>.Success(new ReviewData(dto, list))
            : Result<ReviewData>.Failure(types.Error, types.Message ?? string.Empty);
    }

    private void Apply(ReviewData data)
    {
        TodayText = UiFormat.LongDate(data.Today.Today);
        if (data.Today.State == TodayReviewState.Submitted)
        {
            ShowSummary(data.Today.Summary);
            return;
        }

        BuildForm(data.Types);
        Summary = null;
        Mode = ReviewMode.Form;
    }

    private void ShowSummary(ReviewSummaryDto? summary)
    {
        Summary = summary;
        Mode = ReviewMode.Summary;
    }

    private void BuildForm(IReadOnlyList<ActivityTypeDto> types)
    {
        foreach (var old in _items)
        {
            old.Changed -= OnItemChanged;
        }

        _types = types;
        _items = types.OrderBy(t => t.DisplayOrder).Select(ActivityInputItemFactory.Create).ToList();
        foreach (var item in _items)
        {
            item.Changed += OnItemChanged;
        }

        Groups = _items
            .GroupBy(i => i.CategoryId)
            .Select(g => new ActivityGroupViewModel(g.Key, g.First().Type.CategoryName, g.ToList()))
            .ToList();
        _impacts.Clear();
        _changed.Clear();
        _validation = new ReviewValidationResult([]) { IsEmpty = true };
        _total = 0m;
        TotalText = ImpactText(_total);
        FilledCount = 0;
        HasBlockingError = false;
    }

    private async void OnItemChanged(object? sender, EventArgs e)
    {
        if (sender is ActivityInputItemViewModel item)
        {
            _changed.Add(item);
        }

        var previous = _debounce;
        var current = _debounce = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(_debounceDelay, current.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Revalidate();
    }

    private void Revalidate()
    {
        _debounce?.Cancel();
        _validation = _review.Validate(_items.Select(i => i.ToActivityInput()).ToList(), _types);
        foreach (var item in _items)
        {
            item.ApplyIssues(_validation.Issues);
        }

        foreach (var item in _changed)
        {
            var impact = _review.Preview(item.ToActivityInput(), item.Type);
            item.ApplyImpact(impact);
            if (impact is null)
            {
                _impacts.Remove(item.ActivityTypeId);
            }
            else
            {
                _impacts[item.ActivityTypeId] = impact;
            }
        }

        _changed.Clear();
        _total = _impacts.Values.Sum(i => i.Co2SavedKg);
        TotalText = ImpactText(_total);
        FilledCount = _items.Count(i => i.IsFilled);
        HasBlockingError = _validation.HasErrors;
    }

    private async Task<bool> ConfirmAsync()
    {
        var warnings = _validation.Issues
            .Where(i => i.Severity == ValidationSeverity.Warning)
            .Select(i => $"{_types.First(t => t.ActivityTypeId == i.ActivityTypeId).TypeName}: {i.Message}")
            .Distinct()
            .ToList();
        _warningsAcknowledged = warnings.Count > 0;

        return (warnings.Count == 0 || await _dialogs.ConfirmAsync(
                _text.Get("Dialog.Warning.Title"), string.Join(Environment.NewLine, warnings),
                _text.Get("Dialog.Warning.Confirm"), _text.Get("Dialog.Warning.Cancel")))
            && (!_validation.IsEmpty || await _dialogs.ConfirmAsync(
                _text.Get("Dialog.Empty.Title"), _text.Get("Review.EmptyConfirm"), _text.Get("Dialog.Empty.Confirm")))
            && await _dialogs.ConfirmAsync(
                _text.Get("Dialog.Save.Title"), SaveConfirmationText(), _text.Get("Dialog.Save.Confirm"));
    }

    private string SaveConfirmationText()
    {
        var total = string.Format(CultureInfo.CurrentCulture, _text.Get("Review.ConfirmTotal"), UiFormat.Co2(_total), FilledCount);
        return _text.Get("Review.Confirm") + Environment.NewLine + total;
    }

    private async Task SaveAsync()
    {
        var request = new SubmitReviewRequest(_items.Select(i => i.ToActivityInput()).ToList(), _warningsAcknowledged);
        Result? failure = null;

        // Silent keeps the form on screen while saving; a failed result is shown as a banner, not as the full-page error state.
        var summary = await RunAsync(async ct =>
        {
            var result = await _review.SubmitAsync(request, ct);
            if (!result.IsSuccess)
            {
                failure = result;
            }

            return result;
        }, silent: true);

        if (summary is not null)
        {
            ShowSummary(summary);
            await _shell.RefreshPendingAsync();
        }
        else if (failure is not null)
        {
            await HandleFailureAsync(failure);
        }
    }

    private async Task HandleFailureAsync(Result failure)
    {
        State = LoadState.Loaded;
        ErrorMessage = null;

        switch (failure.Error)
        {
            case ErrorCode.ReviewAlreadySubmitted:
                await LoadAsync(silent: true, LoadToken);
                return;
            case ErrorCode.ReviewDateMismatch:
                await LoadAsync(silent: true, LoadToken);
                break;
            case ErrorCode.ValidationFailed or ErrorCode.WarningsNotAcknowledged:
                Revalidate();
                break;
            case ErrorCode.NetworkError:
                BannerRetryCommand = RetrySaveCommand;
                break;
        }

        BannerMessage = failure.Message;
    }

    private void ClearBanner()
    {
        BannerMessage = null;
        BannerRetryCommand = null;
    }

    private static string ImpactText(decimal kg) => kg == 0m ? UiFormat.Co2(0m) : $"≈ {UiFormat.Co2(kg)}";

    private sealed record ReviewData(TodayReviewDto Today, IReadOnlyList<ActivityTypeDto> Types);
}
