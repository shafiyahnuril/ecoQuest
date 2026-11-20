using System.Globalization;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>One activity of a history day. Every number and sentence comes from the impact the service returned (UIR-60, UIR-62).</summary>
public sealed class HistoryLineItemViewModel
{
    private readonly string? _relativePhotoPath;
    private readonly Func<string?, string?> _resolvePhoto;
    private string? _photoFilePath;
    private bool _photoResolved;

    public HistoryLineItemViewModel(ActivityImpactDto impact, string? description, IUiText text, Func<string?, string?> resolvePhoto)
    {
        _relativePhotoPath = impact.PhotoPath;
        _resolvePhoto = resolvePhoto;
        IconKey = ActivityVisual.For(impact.ActivityTypeId).IconKey;
        Name = impact.TypeName;
        QuantityText = ActivityQuantityText.Of(impact, text);
        Co2Text = UiFormat.Kg(impact.Co2SavedKg);
        DetailCo2Text = UiFormat.Co2(impact.Co2SavedKg);
        PointsText = string.Format(CultureInfo.CurrentCulture, text.Get("History.Points"), UiFormat.Count(impact.PointsEarned));
        FormulaText = ImpactExplanation.Of(impact);
        Note = string.IsNullOrWhiteSpace(impact.Note) ? null : impact.Note;
        Description = description;
    }

    public string IconKey { get; }

    public string Name { get; }

    public string QuantityText { get; }

    public string Co2Text { get; }

    public string DetailCo2Text { get; }

    public string PointsText { get; }

    public string FormulaText { get; }

    public string? Note { get; }

    public bool HasNote => Note is not null;

    public string? Description { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasPhoto => _relativePhotoPath is not null;

    /// <summary>Absolute path of the photo, or null when the file is not on this device. Resolved on first use so a long list does not touch the disk.</summary>
    public string? PhotoFilePath
    {
        get
        {
            if (!_photoResolved)
            {
                _photoFilePath = _resolvePhoto(_relativePhotoPath);
                _photoResolved = true;
            }

            return _photoFilePath;
        }
    }
}
