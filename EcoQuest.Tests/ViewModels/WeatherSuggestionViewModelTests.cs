using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>WeatherSuggestionViewModel (FR-6, AC-4.2): skeleton, live data, generic fallback, timeout and cancellation. It never shows an error.</summary>
public class WeatherSuggestionViewModelTests
{
    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Weather.CityLine"] = "{0} · Saran berdasarkan kota domisili",
        ["Weather.TomorrowIn"] = "Besok di {0}",
        ["Weather.FallbackTitle"] = "Saran umum",
        ["Weather.FallbackFooter"] = "Saran umum, tanpa data cuaca langsung",
        ["Weather.FallbackAdvice"] = "Siapkan tumbler dan tas belanja.",
        ["Weather.Disclaimer.Live"] = "Prakiraan AccuWeather untuk {0}",
        ["Weather.Impact"] = "≈ {0} dihindari",
    };

    private readonly IWeatherSuggestionService _service = Substitute.For<IWeatherSuggestionService>();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero));

    public WeatherSuggestionViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));
        _session.Current.Returns(new UserSession(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null));
    }

    private WeatherSuggestionViewModel Create(TimeSpan? timeout = null) => new(_service, _session, _clock, _text, timeout);

    private static WeatherSuggestionDto Live() =>
        new("Yogyakarta", new DateOnly(2026, 10, 25), false, "Cerah berawan, 24–31°C", 3, "Cocok untuk jalan kaki.", 1, 0.30m);

    private static WeatherSuggestionDto Generic() =>
        new("Yogyakarta", new DateOnly(2026, 10, 25), true, null, null, "Coba bawa tumbler.", 5, 0.10m);

    [Fact]
    public async Task Shows_a_skeleton_until_the_answer_arrives_then_the_live_forecast()
    {
        var pending = new TaskCompletionSource<WeatherSuggestionDto>();
        _service.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(pending.Task);
        var vm = Create();

        vm.StartLoading();

        Assert.True(vm.IsLoading);
        pending.SetResult(Live());
        await vm.LoadTask;

        Assert.False(vm.IsLoading);
        Assert.False(vm.IsFallback);
        Assert.Equal("Besok di Yogyakarta", vm.ContentTitle);
        Assert.Equal("Cerah berawan, 24–31°C", vm.ContentSubtitle);
        Assert.Equal("Cocok untuk jalan kaki.", vm.AdviceText);
        Assert.Equal("≈ 0,30 kg CO₂ dihindari", vm.ImpactText);
        Assert.Equal("Prakiraan AccuWeather untuk Yogyakarta", vm.FooterText);
    }

    [Fact]
    public async Task A_fallback_suggestion_is_shown_as_Saran_umum_without_forecast_text()
    {
        _service.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(Generic());
        var vm = Create();

        vm.StartLoading();
        await vm.LoadTask;

        Assert.True(vm.IsFallback);
        Assert.Equal("Saran umum", vm.ContentTitle);
        Assert.False(vm.HasContentSubtitle);
        Assert.Equal("Saran umum, tanpa data cuaca langsung", vm.FooterText);
    }

    [Fact]
    public async Task No_answer_within_the_timeout_ends_on_the_generic_suggestion_even_if_the_service_ignores_the_token()
    {
        _service.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<WeatherSuggestionDto>().Task);
        var vm = Create(TimeSpan.FromMilliseconds(30));

        vm.StartLoading();
        await vm.LoadTask;

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsFallback);
        Assert.Equal("Saran umum", vm.ContentTitle);
        Assert.Equal("Siapkan tumbler dan tas belanja.", vm.AdviceText);
        Assert.False(vm.HasImpact);
    }

    [Fact]
    public async Task Cancel_stops_the_call_and_drops_a_late_answer()
    {
        CancellationToken seen = default;
        var pending = new TaskCompletionSource<WeatherSuggestionDto>();
        _service.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            seen = call.Arg<CancellationToken>();
            return pending.Task;
        });
        var vm = Create();
        vm.StartLoading();

        vm.Cancel();
        pending.SetResult(Live());
        await vm.LoadTask;

        Assert.True(seen.IsCancellationRequested);
        Assert.False(vm.IsLoading);
        Assert.Equal(string.Empty, vm.ContentTitle);
    }

    [Fact]
    public async Task Starting_again_replaces_the_previous_load()
    {
        var first = new TaskCompletionSource<WeatherSuggestionDto>();
        var second = new TaskCompletionSource<WeatherSuggestionDto>();
        _service.GetTomorrowSuggestionAsync(Arg.Any<CancellationToken>()).Returns(first.Task, second.Task);
        var vm = Create();

        vm.StartLoading();
        var firstLoad = vm.LoadTask;
        vm.StartLoading();
        first.SetResult(Generic());
        second.SetResult(Live());
        await Task.WhenAll(firstLoad, vm.LoadTask);

        Assert.False(vm.IsFallback);
        Assert.Equal("Besok di Yogyakarta", vm.ContentTitle);
    }
}
