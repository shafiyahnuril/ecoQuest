using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.ViewModels.Items;

namespace EcoQuest.ViewModels;

/// <summary>
/// History / Riwayat (S-07). Filter changes are debounced and the last one wins: a newer load cancels the older one and a cancelled load
/// never touches State, so the list does not flicker. The view model is transient; categories and activity descriptions are optional extras,
/// so failing to load them never fails the screen.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    private const int WeekDays = 7;
    private const int MonthDays = 30;

    private readonly IHistoryService _history;
    private readonly IReferenceDataService _reference;
    private readonly IPhotoStorageService _photos;
    private readonly IDialogService _dialogs;
    private readonly IClock _clock;
    private readonly INavigationService _navigation;
    private readonly IUiText _text;
    private readonly TimeSpan _debounce;
    private IReadOnlyDictionary<int, string> _descriptions = new Dictionary<int, string>();
    private CancellationTokenSource? _loadCts;
    private bool _referenceLoaded;
    private bool _applyingPeriod;

    public HistoryViewModel(
        IHistoryService history, IReferenceDataService reference, IPhotoStorageService photos, IDialogService dialogs,
        IClock clock, INavigationService navigation, IUiText text, TimeSpan? debounce = null)
    {
        _history = history;
        _reference = reference;
        _photos = photos;
        _dialogs = dialogs;
        _clock = clock;
        _navigation = navigation;
        _text = text;
        _debounce = debounce ?? DefaultDebounce;
        TodayText = UiFormat.LongDate(clock.Today);
        (_from, _to) = RangeOf(HistoryPeriod.Month);
    }

    public string TodayText { get; }

    /// <summary>The load that is currently running or waiting for its debounce; lets callers (and tests) wait for the screen to settle.</summary>
    public Task PendingLoad { get; private set; } = Task.CompletedTask;

    [ObservableProperty] private HistoryPeriod _period = HistoryPeriod.Month;

    [ObservableProperty] private DateOnly? _from;

    [ObservableProperty] private DateOnly? _to;

    /// <summary>Shown under the date pickers; null when the range is valid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRangeError))]
    private string? _rangeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategories))]
    [NotifyPropertyChangedFor(nameof(AllCategoriesSelected))]
    private IReadOnlyList<HistoryCategoryChip> _categories = [];

    [ObservableProperty] private IReadOnlyList<HistoryDayItemViewModel> _days = [];

    [ObservableProperty] private HistoryDayItemViewModel? _selectedDay;

    public bool HasRangeError => RangeError is not null;

    /// <summary>Opens the D-05 preview; a photo that is not on this device has no file to show, the detail panel already says so.</summary>
    [RelayCommand]
    private void PreviewPhoto(HistoryLineItemViewModel? line)
    {
        if (line?.PhotoFilePath is { } path)
        {
            _dialogs.ShowImagePreview(path);
        }
    }

    public bool HasCategories => Categories.Count > 0;

    /// <summary>No category chip selected means every category.</summary>
    public bool AllCategoriesSelected => Categories.All(c => !c.IsSelected);

    public override Task OnNavigatedToAsync(object? parameter)
    {
        State = LoadState.Loading;
        return PendingLoad = RunLoadAsync(TimeSpan.Zero, BeginLoad());
    }

    public override void OnNavigatedFrom() => _loadCts?.Cancel();

    [RelayCommand]
    private Task LoadAsync() => PendingLoad = RunLoadAsync(TimeSpan.Zero, BeginLoad());

    [RelayCommand]
    private void SetPeriod(HistoryPeriod period) => Period = period;

    [RelayCommand]
    private void SelectAllCategories()
    {
        foreach (var chip in Categories)
        {
            chip.IsSelected = false;
        }
    }

    [RelayCommand]
    private Task GoToReviewAsync() => _navigation.NavigateTo<EveningReviewViewModel>();

    partial void OnPeriodChanged(HistoryPeriod value)
    {
        if (value == HistoryPeriod.Custom)
        {
            return;
        }

        _applyingPeriod = true;
        (From, To) = RangeOf(value);
        _applyingPeriod = false;
        OnFilterChanged();
    }

    partial void OnFromChanged(DateOnly? value) => OnDateEdited();

    partial void OnToChanged(DateOnly? value) => OnDateEdited();

    private void OnDateEdited()
    {
        if (_applyingPeriod)
        {
            return;
        }

        Period = HistoryPeriod.Custom;
        OnFilterChanged();
    }

    private void OnCategorySelectionChanged()
    {
        OnPropertyChanged(nameof(AllCategoriesSelected));
        OnFilterChanged();
    }

    private void OnFilterChanged()
    {
        RangeError = From > To ? _text.Get("History.RangeError") : null;
        if (RangeError is not null)
        {
            _loadCts?.Cancel();
            return;
        }

        PendingLoad = RunLoadAsync(_debounce, BeginLoad());
    }

    private (DateOnly? From, DateOnly? To) RangeOf(HistoryPeriod period)
    {
        var today = _clock.Today;
        return period switch
        {
            HistoryPeriod.Week => (today.AddDays(1 - WeekDays), today),
            HistoryPeriod.Month => (today.AddDays(1 - MonthDays), today),
            _ => (null, null),
        };
    }

    private CancellationToken BeginLoad()
    {
        var previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        return _loadCts.Token;
    }

    private async Task RunLoadAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }

            if (!_referenceLoaded)
            {
                await LoadReferenceDataAsync(ct);
            }

            await LoadHistoryAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task LoadReferenceDataAsync(CancellationToken ct)
    {
        var categories = await _reference.GetCategoriesAsync(ct);
        var types = await _reference.GetActivityTypesAsync(ct);
        ct.ThrowIfCancellationRequested();

        _referenceLoaded = categories.IsSuccess && types.IsSuccess;
        if (categories.IsSuccess)
        {
            Categories = categories.Value!.Select(c => new HistoryCategoryChip(c.CategoryId, c.CategoryName, OnCategorySelectionChanged)).ToList();
        }

        if (types.IsSuccess)
        {
            _descriptions = types.Value!
                .Where(t => !string.IsNullOrWhiteSpace(t.Description))
                .ToDictionary(t => t.ActivityTypeId, t => t.Description!);
        }
    }

    private async Task LoadHistoryAsync(CancellationToken ct)
    {
        if (State != LoadState.Loaded)
        {
            ErrorMessage = null;
            State = LoadState.Loading;
        }

        var filter = new HistoryFilter(From, To, Categories.Where(c => c.IsSelected).Select(c => c.CategoryId).ToList());
        var result = await _history.GetHistoryAsync(filter, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (result.IsSuccess)
        {
            Apply(result.Value!);
        }
        else if (result.FieldErrors.TryGetValue("From", out var rangeError))
        {
            RangeError = rangeError;
            State = LoadState.Loaded;
        }
        else
        {
            ErrorMessage = result.Message;
            State = LoadState.Error;
        }
    }

    private void Apply(IReadOnlyList<HistoryDayDto> days)
    {
        var previousId = SelectedDay?.ReviewId;
        var items = days.Select(d => new HistoryDayItemViewModel(d, _descriptions, _text, _photos.ResolveAbsolutePath)).ToList();

        Days = items;
        SelectedDay = items.FirstOrDefault(d => d.ReviewId == previousId) ?? items.FirstOrDefault();
        IsEmpty = items.Count == 0;
        ErrorMessage = null;
        State = LoadState.Loaded;
    }
}
