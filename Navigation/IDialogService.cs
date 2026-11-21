namespace EcoQuest.Navigation;

public enum DialogIcon { Info, Warning, Leaf, Level, Badge }

/// <summary>Dialogs, file pickers and toasts, so ViewModels stay free of System.Windows (SDD-Frontend 2.3).</summary>
public interface IDialogService
{
    /// <param name="cancelText">Defaults to the shared "Batal" copy.</param>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null, DialogIcon icon = DialogIcon.Info);

    /// <param name="buttonText">Defaults to the shared "Lanjut" copy.</param>
    /// <remarks>Opens after the pending render pass, so the screen that triggered it is already visible behind the dialog.</remarks>
    Task ShowInfoAsync(string title, string message, string? buttonText = null, DialogIcon icon = DialogIcon.Info);

    void ShowToast(string message);

    /// <summary>DEBUG builds only: opens the FE-01 theme preview in its own window. Does nothing in Release.</summary>
    void ShowThemePreview();

    /// <summary>Opens the JPG/PNG picker and returns the chosen path, or null when the user cancels. Validation stays with IPhotoStorageService.</summary>
    string? PickImageFile();

    /// <summary>Opens the large photo preview (D-05). A missing file shows the "Photo.Missing" placeholder instead of an image.</summary>
    void ShowImagePreview(string absolutePath);

    /// <summary>Opens the CSV save dialog (overwrite prompt on) and returns the chosen path, or null when the user cancels.</summary>
    string? PickCsvSaveLocation(string suggestedFileName);
}

/// <summary>Looks up Indonesian UI copy from Resources/Strings.id.xaml by key, so ViewModels never hold literals.</summary>
public interface IUiText
{
    string Get(string key);
}
