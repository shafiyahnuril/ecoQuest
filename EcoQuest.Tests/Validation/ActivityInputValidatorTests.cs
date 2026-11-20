using EcoQuest.Core.Calculation;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Core.Validation;
using Xunit;
using static EcoQuest.Tests.Calculation.CalculationTestData;

namespace EcoQuest.Tests.Validators;

public class ActivityInputValidatorTests
{
    private static readonly IReadOnlyList<ActivityType> Types =
        [Walk(), Transit(), Lamp(), Sorting(), Tumbler(), Bags()];

    private readonly ActivityInputValidator _sut = new(new ImpactCalculatorFactory(new IImpactCalculator[]
    {
        new DistanceImpactCalculator(), new DurationImpactCalculator(),
        new CountImpactCalculator(), new ToggleImpactCalculator()
    }));

    private ReviewValidationResult Check(params ActivityInput[] items) => _sut.Validate(items, Types);

    

    [Fact]
    public void Zero_and_empty_are_valid_not_filled()
    {
        var r = Check(Qty(Walk(), 0m), Qty(Sorting(), null), Yes(Tumbler(), false));
        Assert.Empty(r.Issues);
        Assert.True(r.IsEmpty);
    }

    [Fact]
    public void Above_plausible_is_warning_not_error()
    {
        var r = Check(Qty(Walk(), 45m));
        var issue = Assert.Single(r.Issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.False(r.HasErrors);
    }

    [Fact]
    public void Note_without_quantity_is_error()
    {
        var r = Check(Qty(Walk(), null) with { Note = "catatan saja" });
        var issue = Assert.Single(r.Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Isi aktivitas terlebih dahulu sebelum menambah catatan/foto", issue.Message);
    }

    [Fact]
    public void Note_over_500_is_error()
    {
        var r = Check(Qty(Walk(), 1m) with { Note = new string('a', 501) });
        var issue = Assert.Single(r.Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Note", issue.Field);
    }

    [Fact]
    public void IsEmpty_when_nothing_filled()
    {
        Assert.True(Check(ActivityInput.Empty(1), ActivityInput.Empty(5)).IsEmpty);
        Assert.False(Check(Qty(Walk(), 1m)).IsEmpty);
    }

    

    [Fact]
    public void Walk_negative_is_error()
    {
        var issue = Assert.Single(Check(Qty(Walk(), -1m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Jarak tidak boleh negatif", issue.Message);
    }

    [Fact]
    public void Walk_three_decimals_is_error()
    {
        var issue = Assert.Single(Check(Qty(Walk(), 1.234m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Maksimal 2 angka di belakang koma", issue.Message);
    }

    [Fact]
    public void Transit_1200km_is_error_above_hard_max()
    {
        var issue = Assert.Single(Check(Qty(Transit(), 1200m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Melebihi batas maksimum 1.000 km per hari", issue.Message);
    }

    

    [Fact]
    public void Lamp_only_start_is_error()
    {
        var issue = Assert.Single(Check(Time(Lamp(), new TimeOnly(20, 0), null)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Lengkapi jam selesai", issue.Message);
    }

    [Fact]
    public void Lamp_equal_start_end_is_error()
    {
        var issue = Assert.Single(Check(Time(Lamp(), new TimeOnly(20, 0), new TimeOnly(20, 0))).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Rentang jam tidak boleh 0", issue.Message);
    }

    [Fact]
    public void Lamp_above_12_hours_is_warning()
    {
        var r = Check(Time(Lamp(), new TimeOnly(6, 0), new TimeOnly(19, 0)));
        var issue = Assert.Single(r.Issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.False(r.HasErrors);
    }

    
    [Fact]
    public void Negative_count_is_error()
    {
        var issue = Assert.Single(Check(Qty(Sorting(), -1m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Jumlah tidak boleh negatif", issue.Message);
    }

    [Fact]
    public void Sorting_1_5_is_error_whole_number()
    {
        var issue = Assert.Single(Check(Qty(Sorting(), 1.5m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Masukkan bilangan bulat", issue.Message);
    }

    [Fact]
    public void Sorting_51_is_error_above_hard_max()
    {
        var issue = Assert.Single(Check(Qty(Sorting(), 51m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Melebihi batas maksimum 50 kali per hari", issue.Message);
    }

    [Fact]
    public void Sorting_11_is_warning_above_plausible()
    {
        var r = Check(Qty(Sorting(), 11m));
        var issue = Assert.Single(r.Issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.False(r.HasErrors);
    }

    
    [Fact]
    public void Toggle_value_other_than_0_or_1_is_error()
    {
        var issue = Assert.Single(Check(Qty(Tumbler(), 2m)).Issues);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Pilih Ya atau Tidak", issue.Message);
    }
}
