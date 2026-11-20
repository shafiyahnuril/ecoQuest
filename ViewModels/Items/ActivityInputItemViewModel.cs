using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;

namespace EcoQuest.ViewModels.Items;

/// <summary>
/// One Evening Review row. Holds raw input and the text the service returned for it; it never computes or validates (UIR-33).
/// <see cref="Changed"/> fires on every edit, throttling is up to the owner of the list.
/// </summary>
public abstract partial class ActivityInputItemViewModel : ObservableObject
{
    private static readonly string[] NoteFields = ["Note"];
    private static readonly string[] PhotoFields = ["Photo"];

    private readonly ActivityVisual _visual;

    [ObservableProperty] private string? _note;
    [ObservableProperty] private string? _photoSourcePath;
    [ObservableProperty] private string _impactText = UiFormat.Co2(0m);
    [ObservableProperty] private string? _explanationText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError), nameof(HasWarning))] private string? _errorText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWarning))] private string? _warningText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNoteError), nameof(ShowExtras))] private string? _noteErrorText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowExtras))] private bool _isExtrasExpanded;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPhotoError), nameof(PhotoMessageText), nameof(HasPhotoMessage))] private string? _photoErrorText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PhotoMessageText), nameof(HasPhotoMessage))] private string? _photoPickErrorText;

    private IDialogService? _dialogs;
    private IPhotoStorageService? _photos;

    protected ActivityInputItemViewModel(ActivityTypeDto type)
    {
        Type = type;
        _visual = ActivityVisual.For(type.ActivityTypeId);
        _explanationText = ImpactExplanation.Of(null, type.Description);
    }

    public event EventHandler? Changed;

    public ActivityTypeDto Type { get; }

    public int ActivityTypeId => Type.ActivityTypeId;

    public int CategoryId => Type.CategoryId;

    public string TypeName => Type.TypeName;

    public string? Description => Type.Description;

    public string UnitLabel => Type.UnitLabel;

    public InputControlType InputControlType => Type.InputControlType;

    public string DescriptionKey => $"Activity.Desc.{ActivityTypeId}";

    public string IconKey => _visual.IconKey;

    public string BubbleBackgroundKey => _visual.BubbleBackgroundKey;

    public string BubbleForegroundKey => _visual.BubbleForegroundKey;

    public bool HasError => ErrorText is not null;

    public bool HasWarning => WarningText is not null && !HasError;

    public bool HasNoteError => NoteErrorText is not null;

    public bool HasPhotoError => PhotoErrorText is not null;

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoSourcePath);

    /// <summary>The picker's own failure wins over the last review validation, because validation clears its text on every edit.</summary>
    public string? PhotoMessageText => PhotoPickErrorText ?? PhotoErrorText;

    public bool HasPhotoMessage => PhotoMessageText is not null;

    /// <summary>The note area opens on request once the row is filled; a note error always shows it so the message is never hidden.</summary>
    public bool ShowExtras => (IsFilled && IsExtrasExpanded) || HasNoteError;

    public abstract bool IsFilled { get; }

    protected abstract IReadOnlyCollection<string> ControlFields { get; }

    public abstract ActivityInput ToActivityInput();

    /// <summary>Gives the row what it needs to pick and preview a photo. Without it the photo commands stay disabled.</summary>
    public void ConfigurePhoto(IDialogService dialogs, IPhotoStorageService photos)
    {
        _dialogs = dialogs;
        _photos = photos;
        PickPhotoCommand.NotifyCanExecuteChanged();
        PreviewPhotoCommand.NotifyCanExecuteChanged();
    }

    public void ApplyIssues(IEnumerable<ValidationIssue> issues)
    {
        var own = issues.Where(i => i.ActivityTypeId == ActivityTypeId).ToList();

        ErrorText = FirstMessage(own, ValidationSeverity.Error, ControlFields);
        WarningText = FirstMessage(own, ValidationSeverity.Warning, ControlFields);
        NoteErrorText = FirstMessage(own, ValidationSeverity.Error, NoteFields);
        PhotoErrorText = FirstMessage(own, ValidationSeverity.Error, PhotoFields);
    }

    public void ApplyImpact(ActivityImpactDto? impact)
    {
        ImpactText = impact is { Co2SavedKg: not 0m } ? $"≈ {UiFormat.Co2(impact.Co2SavedKg)}" : UiFormat.Co2(0m);
        ExplanationText = ImpactExplanation.Of(impact, Description);
    }

    protected void NotifyInputChanged()
    {
        OnPropertyChanged(nameof(IsFilled));
        OnPropertyChanged(nameof(ShowExtras));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleExtras() => IsExtrasExpanded = !IsExtrasExpanded;

    // Add and replace share one flow; a cancelled picker changes nothing and a rejected file is not attached.
    [RelayCommand(CanExecute = nameof(CanPickPhoto))]
    private void PickPhoto()
    {
        var path = _dialogs!.PickImageFile();
        if (path is null)
        {
            return;
        }

        var check = _photos!.ValidatePhoto(path);
        if (!check.IsSuccess)
        {
            PhotoPickErrorText = check.Message;
            return;
        }

        PhotoPickErrorText = null;
        PhotoSourcePath = path;
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        PhotoPickErrorText = null;
        PhotoSourcePath = null;
    }

    [RelayCommand(CanExecute = nameof(CanPreviewPhoto))]
    private void PreviewPhoto() => _dialogs!.ShowImagePreview(PhotoSourcePath!);

    private bool CanPickPhoto() => _dialogs is not null && _photos is not null;

    private bool CanPreviewPhoto() => _dialogs is not null && HasPhoto;

    partial void OnNoteChanged(string? value) => NotifyInputChanged();

    partial void OnPhotoSourcePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        PreviewPhotoCommand.NotifyCanExecuteChanged();
        NotifyInputChanged();
    }

    private static string? FirstMessage(IEnumerable<ValidationIssue> issues, ValidationSeverity severity, IReadOnlyCollection<string> fields) =>
        issues.FirstOrDefault(i => i.Severity == severity && fields.Contains(i.Field))?.Message;
}
