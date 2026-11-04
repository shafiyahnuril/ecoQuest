using System.Configuration;
using System.Data;
using System.Windows;
using DotNetEnv;
using ecoQuest.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ecoQuest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // FE-02 composition root: build the presentation DI container, show the shell, start on Login.
            // Placed before the legacy Supabase block so the UI never waits on the network.
            var services = new ServiceCollection().AddEcoQuestPresentation();
            // FE fake services (ADR-018): the flag is read from the process env or the nearest .env by FakeSettings
            // itself, because Env.Load() below runs AFTER this container is built.
            if (FakesRegistration.IsEnabled()) services.AddEcoQuestFakes();
            _services = services.BuildServiceProvider();
            var shell = _services.GetRequiredService<ShellViewModel>();
            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = shell;
            mainWindow.Show();
            await _services.GetRequiredService<INavigationService>().NavigateTo<LoginViewModel>();

            // TEMP until BE-01 removes the Supabase seed block below: in fake mode the app must run without .env or
            // Supabase keys, and the legacy block would insert a plaintext-password test user and show MessageBoxes.
            if (FakesRegistration.IsEnabled()) return;

            // Load environment variables dari file .env
            Env.Load();

            // Inisialisasi Supabase
            try 
            {
                await SupabaseService.Instance.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal terhubung ke Supabase: {ex.Message}");
            }
        }
    }
}
