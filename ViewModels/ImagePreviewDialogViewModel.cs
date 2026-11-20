using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EcoQuest.ViewModels;

/// <summary>Content of <c>Views/Dialogs/ImagePreviewDialog</c> (D-05). <see cref="Result"/> closes the window (DialogHelper).</summary>
public sealed partial class ImagePreviewDialogViewModel(string filePath, string closeText) : ObservableObject
{
    public string FilePath { get; } = filePath;

    public string CloseText { get; } = closeText;

    /// <summary>False when the file is gone, so the dialog shows the "Photo.Missing" placeholder.</summary>
    public bool HasImage { get; } = File.Exists(filePath);

    [ObservableProperty] private bool? _result;

    [RelayCommand]
    private void Close() => Result = true;
}
