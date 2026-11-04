namespace EcoQuest.Navigation;

/// <summary>Dialogs, file pickers and toasts, so ViewModels stay free of System.Windows (SDD-Frontend 2.3).</summary>
public interface IDialogService
{
    /// <param name="cancelText">Defaults to the shared "Batal" copy.</param>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string? cancelText = null);

    /// <param name="buttonText">Defaults to the shared "Lanjut" copy.</param>
    Task ShowInfoAsync(string title, string message, string? buttonText = null);

    void ShowToast(string message);

    /// <summary>DEBUG builds only: opens the FE-01 theme preview in its own window. Does nothing in Release.</summary>
    void ShowThemePreview();

    // TODO(FE-14/FE-19): string? PickImageFile(); string? PickCsvSaveLocation(string suggestedFileName);
}

/// <summary>Looks up Indonesian UI copy from Resources/Strings.id.xaml by key, so ViewModels never hold literals.</summary>
public interface IUiText
{
    string Get(string key);
}
