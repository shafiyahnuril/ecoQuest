using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels.Items;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class ActivityInputItemTests
{
    private static ActivityTypeDto Type(int id) => FakeData.ActivityTypes.Single(t => t.ActivityTypeId == id);

    private static T Item<T>(int id) where T : ActivityInputItemViewModel => (T)ActivityInputItemFactory.Create(Type(id));

    private static ActivityImpactDto Impact(int id, decimal co2, string formula = "3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂") =>
        new(null, id, "x", 1, "x", InputControlType.Numeric, 3m, "km", null, null, co2, 9, 0.15m, 1m, formula, "0,45 × 20 × 1,0 = 9 poin", null, null);

    private static ValidationIssue Issue(int id, string field, ValidationSeverity severity, string message) => new(id, field, severity, message);

    [Theory]
    [InlineData(1, typeof(NumericItemViewModel))]
    [InlineData(2, typeof(NumericItemViewModel))]
    [InlineData(3, typeof(TimeRangeItemViewModel))]
    [InlineData(4, typeof(CountItemViewModel))]
    [InlineData(5, typeof(ToggleItemViewModel))]
    [InlineData(6, typeof(CountItemViewModel))]
    public void Factory_creates_the_subclass_of_each_seed_activity(int id, Type expected)
    {
        Assert.IsType(expected, ActivityInputItemFactory.Create(Type(id)));
    }

    [Fact]
    public void Factory_rejects_an_unknown_control_type()
    {
        var unknown = Type(1) with { InputControlType = (InputControlType)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => ActivityInputItemFactory.Create(unknown));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void New_items_are_empty(int id)
    {
        var item = ActivityInputItemFactory.Create(Type(id));

        Assert.False(item.IsFilled);
        Assert.Equal(ActivityInput.Empty(id), item.ToActivityInput());
        Assert.Equal("0 kg CO₂", item.ImpactText);
    }

    [Fact]
    public void Numeric_item_maps_quantity_note_and_photo()
    {
        var item = Item<NumericItemViewModel>(1);
        item.Quantity = 3.5m;
        item.Note = "pagi";
        item.PhotoSourcePath = "C:\\a.jpg";

        Assert.Equal(new ActivityInput(1, 3.5m, null, null, null, "pagi", "C:\\a.jpg"), item.ToActivityInput());
        Assert.True(item.IsFilled);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(2, true)]
    public void Count_item_is_filled_only_for_a_non_zero_quantity(int? quantity, bool filled)
    {
        var item = Item<CountItemViewModel>(4);
        item.Quantity = quantity;

        Assert.Equal(filled, item.IsFilled);
        Assert.Equal(quantity, item.ToActivityInput().Quantity);
    }

    [Fact]
    public void Time_range_item_keeps_an_end_before_the_start()
    {
        var item = Item<TimeRangeItemViewModel>(3);
        item.StartTime = new TimeOnly(21, 0);
        item.EndTime = new TimeOnly(0, 30);

        var input = item.ToActivityInput();

        Assert.Equal(new TimeOnly(21, 0), input.StartTime);
        Assert.Equal(new TimeOnly(0, 30), input.EndTime);
        Assert.Null(input.Quantity);
        Assert.True(item.IsFilled);
    }

    [Fact]
    public void Time_range_item_with_only_one_side_is_filled_so_the_service_can_report_the_gap()
    {
        var item = Item<TimeRangeItemViewModel>(3);
        item.StartTime = new TimeOnly(21, 0);

        Assert.True(item.IsFilled);
        Assert.Null(item.ToActivityInput().EndTime);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Toggle_item_is_filled_only_when_yes(bool? isYes, bool filled)
    {
        var item = Item<ToggleItemViewModel>(5);
        item.IsYes = isYes;

        Assert.Equal(filled, item.IsFilled);
        Assert.Equal(isYes, item.ToActivityInput().IsYes);
    }

    [Fact]
    public void Display_properties_come_from_the_type_and_the_visual_table()
    {
        var item = ActivityInputItemFactory.Create(Type(2));

        Assert.Equal("Transportasi Umum", item.TypeName);
        Assert.Equal("km", item.UnitLabel);
        Assert.Equal(1, item.CategoryId);
        Assert.Equal("Activity.Desc.2", item.DescriptionKey);
        Assert.Equal("Icon.Bus", item.IconKey);
        Assert.Equal("Bubble.Blue.Bg", item.BubbleBackgroundKey);
        Assert.Equal("Bubble.Blue.Fg", item.BubbleForegroundKey);
        Assert.Equal(Type(2).Description, item.Description);
    }

    [Fact]
    public void An_unknown_activity_falls_back_to_the_default_icon()
    {
        var item = ActivityInputItemFactory.Create(Type(1) with { ActivityTypeId = 99 });

        Assert.Equal(ActivityVisual.Fallback.IconKey, item.IconKey);
        Assert.Equal(ActivityVisual.Fallback.BubbleBackgroundKey, item.BubbleBackgroundKey);
    }

    [Fact]
    public void Changed_fires_for_every_input_property()
    {
        var numeric = Item<NumericItemViewModel>(1);
        var time = Item<TimeRangeItemViewModel>(3);
        var toggle = Item<ToggleItemViewModel>(5);
        var fired = 0;
        numeric.Changed += (_, _) => fired++;
        time.Changed += (_, _) => fired++;
        toggle.Changed += (_, _) => fired++;

        numeric.Quantity = 1m;
        numeric.Note = "n";
        numeric.PhotoSourcePath = "p.png";
        time.StartTime = new TimeOnly(1, 0);
        time.EndTime = new TimeOnly(2, 0);
        toggle.IsYes = true;

        Assert.Equal(6, fired);
    }

    [Fact]
    public void Changed_does_not_fire_when_the_value_is_the_same()
    {
        var item = Item<NumericItemViewModel>(1);
        item.Quantity = 2m;
        var fired = 0;
        item.Changed += (_, _) => fired++;

        item.Quantity = 2m;

        Assert.Equal(0, fired);
    }

    [Fact]
    public void IsFilled_change_is_notified()
    {
        var item = Item<NumericItemViewModel>(1);
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Quantity = 1m;

        Assert.Contains(nameof(item.IsFilled), raised);
    }

    [Fact]
    public void ApplyIssues_routes_each_field_to_its_control()
    {
        var item = Item<TimeRangeItemViewModel>(3);

        item.ApplyIssues(
        [
            Issue(3, "StartTime", ValidationSeverity.Error, "Isi jam mulai"),
            Issue(3, "Note", ValidationSeverity.Error, "Catatan maksimal 500 karakter"),
            Issue(3, "Photo", ValidationSeverity.Error, "Ukuran foto maksimal 5 MB"),
        ]);

        Assert.Equal("Isi jam mulai", item.ErrorText);
        Assert.Equal("Catatan maksimal 500 karakter", item.NoteErrorText);
        Assert.Equal("Ukuran foto maksimal 5 MB", item.PhotoErrorText);
        Assert.True(item.HasError);
        Assert.True(item.HasNoteError);
        Assert.True(item.HasPhotoError);
    }

    [Theory]
    [InlineData(1, "Quantity")]
    [InlineData(3, "EndTime")]
    [InlineData(3, "TimeRange")]
    [InlineData(5, "IsYes")]
    public void ApplyIssues_maps_control_fields_of_each_kind(int id, string field)
    {
        var item = ActivityInputItemFactory.Create(Type(id));

        item.ApplyIssues([Issue(id, field, ValidationSeverity.Error, "salah")]);

        Assert.Equal("salah", item.ErrorText);
    }

    [Fact]
    public void ApplyIssues_ignores_issues_of_other_activities_and_foreign_fields()
    {
        var item = Item<NumericItemViewModel>(1);

        item.ApplyIssues(
        [
            Issue(2, "Quantity", ValidationSeverity.Error, "milik lain"),
            Issue(1, "StartTime", ValidationSeverity.Error, "bukan kolom ini"),
        ]);

        Assert.Null(item.ErrorText);
        Assert.False(item.HasError);
    }

    [Fact]
    public void An_error_wins_over_a_warning()
    {
        var item = Item<NumericItemViewModel>(1);

        item.ApplyIssues(
        [
            Issue(1, "Quantity", ValidationSeverity.Warning, "tidak biasa"),
            Issue(1, "Quantity", ValidationSeverity.Error, "terlalu besar"),
        ]);

        Assert.Equal("terlalu besar", item.ErrorText);
        Assert.Equal("tidak biasa", item.WarningText);
        Assert.True(item.HasError);
        Assert.False(item.HasWarning);
    }

    [Fact]
    public void A_warning_alone_is_shown_and_clearing_issues_resets_the_row()
    {
        var item = Item<NumericItemViewModel>(1);

        item.ApplyIssues([Issue(1, "Quantity", ValidationSeverity.Warning, "tidak biasa")]);
        Assert.True(item.HasWarning);
        Assert.False(item.HasError);

        item.ApplyIssues([]);
        Assert.Null(item.WarningText);
        Assert.False(item.HasWarning);
    }

    [Fact]
    public void ApplyImpact_formats_the_co2_with_the_service_formula()
    {
        var item = Item<NumericItemViewModel>(1);

        item.ApplyImpact(Impact(1, 0.45m));

        Assert.Equal("≈ 0,45 kg CO₂", item.ImpactText);
        Assert.Equal("3,00 km × 0,15 kg CO₂/km = 0,45 kg CO₂", item.FormulaExplanation);
    }

    [Fact]
    public void ApplyImpact_with_null_or_zero_shows_zero_and_clears_the_formula()
    {
        var item = Item<NumericItemViewModel>(1);
        item.ApplyImpact(Impact(1, 0.45m));

        item.ApplyImpact(null);
        Assert.Equal("0 kg CO₂", item.ImpactText);
        Assert.Null(item.FormulaExplanation);

        item.ApplyImpact(Impact(1, 0m));
        Assert.Equal("0 kg CO₂", item.ImpactText);
    }

    [Fact]
    public void A_note_over_500_characters_is_flagged_by_the_service_rule_and_shown_on_the_note_field()
    {
        var item = Item<NumericItemViewModel>(1);
        item.Quantity = 1m;
        item.Note = new string('a', 501);

        var validation = FakeEveningReviewService.ValidateItems([item.ToActivityInput()], FakeData.ActivityTypes);
        item.ApplyIssues(validation.Issues);

        Assert.Equal("Catatan maksimal 500 karakter", item.NoteErrorText);
        Assert.Null(item.ErrorText);
    }
}
