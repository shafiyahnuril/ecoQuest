using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EcoQuest.ViewModels;

namespace ecoQuest.Views.Dev;

/// <summary>TEMPORARY (with ThemePreviewView, delete in FE-21): lets the preview switch one StateHost between its states by hand.</summary>
public sealed partial class StatePreviewViewModel : ViewModelBase
{
    [ObservableProperty] private int _retryCount;

    public StatePreviewViewModel()
    {
        State = LoadState.Loaded;
    }

    [RelayCommand]
    private void ShowLoading() { State = LoadState.Loading; IsEmpty = false; }

    [RelayCommand]
    private void ShowEmpty() { State = LoadState.Loaded; IsEmpty = true; }

    [RelayCommand]
    private void ShowError() { ErrorMessage = "Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi."; State = LoadState.Error; }

    [RelayCommand]
    private void ShowNormal() { State = LoadState.Loaded; IsEmpty = false; }

    [RelayCommand]
    private void Retry() { RetryCount++; ShowNormal(); }

    [RelayCommand]
    private void ToggleBusy() => IsBusy = !IsBusy;
}
