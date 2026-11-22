using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Tests.TestDoubles;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>ReviewSummaryViewModel and the item texts of S-06 (UIR-39): every figure comes from the DTO, quantities keep the user's own form.</summary>
public class ReviewSummaryViewModelTests
{
    private static readonly DateOnly Day = new(2026, 10, 24);

    private static readonly Dictionary<string, string> Strings = new()
    {
        ["Summary.LockedAt"] = "Terkunci · {0} WIB",
        ["Summary.Points"] = "{0} poin indikatif",
        ["Summary.Streak"] = "Streak saat ini {0} hari",
        ["Summary.Impact"] = "{0} · {1} poin",
        ["Summary.NoActivities"] = "Refleksi tanpa aktivitas tercatat",
        ["Summary.Duration"] = "{0} j {1} m",
        ["Summary.DurationHours"] = "{0} j",
        ["Summary.DurationMinutes"] = "{0} m",
        ["Switch.Yes"] = "Ya",
    };

    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 24, 14, 40, 0, TimeSpan.Zero));

    public ReviewSummaryViewModelTests() =>
        _text.Get(Arg.Any<string>()).Returns(call => Strings.GetValueOrDefault(call.Arg<string>(), call.Arg<string>()));

    private static ActivityImpactDto Impact(
        int typeId, string name, InputControlType control, decimal quantity, string unit, decimal co2, int points,
        TimeOnly? start = null, TimeOnly? end = null) =>
        new(Guid.NewGuid(), typeId, name, 1, "Kategori", control, quantity, unit, start, end, co2, points, 0.15m, 1m,
            "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", "0,45 × 20 × 1,0 = 9 poin", null, null);

    private ReviewSummaryViewModel Create(IReadOnlyList<ActivityImpactDto> items, decimal total = 0.9m, int points = 20) =>
        new(new ReviewSummaryDto(Guid.NewGuid(), Day, _clock.UtcNow, total, points, items, "Insight dari service.", 4, null, []),
            _clock, _text, new WeatherSuggestionViewModel(Substitute.For<IWeatherSuggestionService>(), Substitute.For<ISessionService>(), _clock, _text),
            new AsyncRelayCommand(() => Task.CompletedTask));

    [Fact]
    public void Totals_points_streak_and_insight_are_shown_as_the_service_returned_them()
    {
        var vm = Create([Impact(1, "Jalan Kaki / Bersepeda", InputControlType.Numeric, 3m, "km", 0.45m, 9)]);

        Assert.Equal("0,90 kg", vm.TotalText);
        Assert.Equal("20 poin indikatif", vm.PointsText);
        Assert.Equal("Streak saat ini 4 hari", vm.StreakText);
        Assert.Equal("Insight dari service.", vm.Insight);
        Assert.True(vm.HasItems);
    }

    [Fact]
    public void Locked_pill_converts_the_submit_time_to_WIB()
    {
        var vm = Create([]);

        Assert.Equal("Terkunci · 21:40 WIB", vm.LockedText);
    }

    [Fact]
    public void Each_activity_keeps_its_own_quantity_form_and_the_service_impact()
    {
        var vm = Create(
        [
            Impact(1, "Jalan Kaki", InputControlType.Numeric, 3m, "km", 0.45m, 9),
            Impact(3, "Lampu", InputControlType.TimeRange, 2.5m, "jam", 0.033m, 1, new TimeOnly(21, 0), new TimeOnly(23, 30)),
            Impact(4, "Sampah", InputControlType.Count, 2m, "kali", 0.6m, 14),
            Impact(5, "Tumbler", InputControlType.Toggle, 1m, "kali", 0.1m, 2),
        ]);

        Assert.Equal(["3,00 km", "21:00–23:30 (2 j 30 m)", "2 kali", "Ya"], vm.Items.Select(i => i.QuantityText));
        Assert.Equal(["0,45 kg · 9 poin", "0,033 kg · 1 poin", "0,60 kg · 14 poin", "0,10 kg · 2 poin"], vm.Items.Select(i => i.ImpactText));
    }

    [Theory]
    [InlineData(22, 0, 23, 30, "22:00–23:30 (1 j 30 m)")]
    [InlineData(22, 0, 1, 15, "22:00–01:15 (3 j 15 m)")]
    [InlineData(20, 0, 22, 0, "20:00–22:00 (2 j)")]
    [InlineData(20, 0, 20, 45, "20:00–20:45 (45 m)")]
    public void Time_range_duration_also_works_across_midnight(int sh, int sm, int eh, int em, string expected)
    {
        var impact = Impact(3, "Lampu", InputControlType.TimeRange, 1m, "jam", 0.013m, 1, new TimeOnly(sh, sm), new TimeOnly(eh, em));

        Assert.Equal(expected, ActivityQuantityText.Of(impact, _text));
    }

    [Fact]
    public void The_tooltip_has_two_lines_formula_then_points()
    {
        var vm = Create([Impact(1, "Jalan Kaki", InputControlType.Numeric, 3m, "km", 0.45m, 9)]);

        Assert.Equal(
            "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂" + Environment.NewLine + "0,45 × 20 × 1,0 = 9 poin",
            vm.Items[0].FormulaText);
    }

    [Fact]
    public void A_review_without_activities_is_still_meaningful()
    {
        var vm = Create([], total: 0m, points: 0);

        Assert.False(vm.HasItems);
        Assert.Equal("0 kg", vm.TotalText);
        Assert.Equal("0 poin indikatif", vm.PointsText);
        Assert.Equal("Streak saat ini 4 hari", vm.StreakText);
        Assert.Equal("Refleksi tanpa aktivitas tercatat", vm.EmptyText);
    }
}
