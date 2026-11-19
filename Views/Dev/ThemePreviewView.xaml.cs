using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EcoQuest.Controls;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;

namespace ecoQuest.Views.Dev;

public partial class ThemePreviewView : UserControl
{
    public ThemePreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ActivityItems.ItemsSource = FakeData.ActivityTypes.Select(CreateActivityItem).ToList();
        ShowConnectionErrorVariants();
        ShowDashboardPanelVariants();
        ShowHistoryVariants();
    }

    private void ShowDashboardPanelVariants()
    {
        var text = new ResourceUiText();
        PreviewInsightEmpty.DataContext = new InsightPanelSample(text.Get("Dashboard.InsightSubtitle"),
        [
            InsightRowItem.For(InsightKind.EmptyState, text.Get("Dashboard.InsightEmptyTitle"), text.Get("Dashboard.InsightEmptyBody")),
            InsightRowItem.Tip(text.Get("Dashboard.InsightHabitTitle"), text.Get("Dashboard.InsightHabitBody")),
        ]);
        PreviewInsightFilled.DataContext = new InsightPanelSample(string.Format(text.Get("Dashboard.InsightSubtitleFilled"), 7),
        [
            InsightRowItem.For(InsightKind.TopCategory, text.Get("Dashboard.Insight.TopCategory"), "Kategori Transportasi paling banyak menurunkan emisimu (47% dari total dampakmu)."),
            InsightRowItem.For(InsightKind.WeakestCategory, text.Get("Dashboard.Insight.WeakestCategory"), "Kategori Energi masih kecil kontribusinya. Coba tambah satu kebiasaan di kategori ini."),
            InsightRowItem.For(InsightKind.Streak, text.Get("Dashboard.Insight.Streak"), "Kamu sudah refleksi 4 hari berturut-turut. Pertahankan!"),
        ]);

        var session = new FakeSessionService();
        session.Start(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
        var clock = new SystemClock();
        var tomorrow = clock.Today.AddDays(1);
        PreviewWeatherLive.DataContext = StartWeather(text, session, clock, Task.FromResult(new WeatherSuggestionDto(
            "Yogyakarta", tomorrow, false, "Cerah berawan, 24–31°C", 3,
            "Cuaca besok cerah berawan. Cocok untuk berjalan kaki atau bersepeda ke kampus.", 1, 0.3m)));
        PreviewWeatherFallback.DataContext = StartWeather(text, session, clock, Task.FromResult(new WeatherSuggestionDto(
            "Yogyakarta", tomorrow, true, null, null, text.Get("Weather.FallbackAdvice"), null, null)));
        PreviewWeatherLoading.DataContext = StartWeather(text, session, clock, new TaskCompletionSource<WeatherSuggestionDto>().Task);
    }

    private static WeatherSuggestionViewModel StartWeather(IUiText text, ISessionService session, IClock clock, Task<WeatherSuggestionDto> answer)
    {
        var weather = new WeatherSuggestionViewModel(new PreviewWeatherService(answer), session, clock, text, TimeSpan.FromDays(1));
        weather.StartLoading();
        return weather;
    }

    private void ShowHistoryVariants()
    {
        PreviewHistoryFilled.Content = StartHistory(FakeScenario.Normal, null);
        PreviewHistoryRangeError.Content = StartHistory(FakeScenario.Normal, vm => vm.From = vm.To!.Value.AddDays(1));
        PreviewHistoryEmpty.Content = StartHistory(FakeScenario.Empty, null);
    }

    private static HistoryViewModel StartHistory(FakeScenario scenario, Action<HistoryViewModel>? afterLoad)
    {
        var clock = new SystemClock();
        var context = new FakeContext(scenario, clock);
        var viewModel = new HistoryViewModel(
            new FakeHistoryService(context, new FakeStore(context)), new FakeReferenceDataService(context), new FakePhotoStorageService(),
            clock, new PreviewNavigation(), new ResourceUiText());
        _ = LoadHistoryAsync(viewModel, afterLoad);
        return viewModel;
    }

    private static async Task LoadHistoryAsync(HistoryViewModel viewModel, Action<HistoryViewModel>? afterLoad)
    {
        await viewModel.OnNavigatedToAsync(null);
        afterLoad?.Invoke(viewModel);
    }

    private sealed record InsightPanelSample(string InsightSubtitle, IReadOnlyList<InsightRowItem> InsightRows);

    private sealed class PreviewWeatherService(Task<WeatherSuggestionDto> answer) : IWeatherSuggestionService
    {
        public Task<WeatherSuggestionDto> GetTomorrowSuggestionAsync(CancellationToken ct = default) => answer;
    }

    private void ShowConnectionErrorVariants()
    {
        var text = new ResourceUiText();
        var navigation = new PreviewNavigation();
        PreviewNetworkError.Content = new ConnectionErrorViewModel(new AppConfig(null, null, null, UseFakes: true), navigation, text);
        PreviewConfigError.Content = new ConnectionErrorViewModel(new AppConfig("", " ", null, UseFakes: false), navigation, text);
    }

    private sealed class PreviewNavigation : INavigationService
    {
        public ViewModelBase? Current => null;

        public bool CanGoBack => false;

        public event EventHandler? Navigated { add { } remove { } }

        public Task NavigateTo<TViewModel>(object? parameter = null) where TViewModel : ViewModelBase => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;

        public void ClearHistory() { }
    }

    // Stands in for the Evening Review ViewModel (next task) so Validate/Preview results can be eyeballed.
    private ActivityInputItemViewModel CreateActivityItem(ActivityTypeDto type)
    {
        var item = ActivityInputItemFactory.Create(type);
        item.Changed += (_, _) =>
        {
            var input = item.ToActivityInput();
            var validation = FakeEveningReviewService.ValidateItems([input], FakeData.ActivityTypes);
            item.ApplyIssues(validation.Issues);
            item.ApplyImpact(item.IsFilled && !validation.HasErrors ? FakeImpact.Build(input, type) : null);
        };
        return item;
    }

    // Visual-only: forces the Validation.HasError state so the error borders can be inspected.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MarkInvalid(TxtError, TagProperty, "Username sudah digunakan.");
        MarkInvalid(StepperError, NumericStepper.ValueProperty, "Jarak melebihi batas wajar per hari.");
        MarkInvalid(TimeError, TimeRangePicker.StartTimeProperty, "Waktu selesai harus berbeda dari waktu mulai.");

        TimeFilled.StartTime = new TimeOnly(21, 0);
        TimeFilled.EndTime = new TimeOnly(0, 30);
        TimeDisabled.StartTime = new TimeOnly(21, 0);
        TimeDisabled.EndTime = new TimeOnly(23, 30);
    }

    private void MarkInvalid(FrameworkElement target, DependencyProperty property, string message)
    {
        var binding = new Binding { Source = this, Path = new PropertyPath(nameof(Tag)), Mode = BindingMode.OneWay };
        var expression = BindingOperations.SetBinding(target, property, binding);
        Validation.MarkInvalid(expression, new ValidationError(new ExceptionValidationRule(), expression, message, null));
    }
}
