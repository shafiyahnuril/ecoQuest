using System;
using System.Configuration;
using System.Data;
using System.Windows;
using DotNetEnv;
using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.Services;
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

            Env.TraversePath().Load();
            var config = AppConfig.FromEnvironment();
            var useReal = !config.UseFakes && config.MissingRequiredKeys().Count == 0;

            var services = new ServiceCollection().AddEcoQuestPresentation();
            services.AddSingleton(config);
            services.AddEcoQuestFakes();
            if (useReal)
            {
                services.AddEcoQuestBackend();
            }

            _services = services.BuildServiceProvider();

            if (useReal)
            {
                try
                {
                    await _services.GetRequiredService<SupabaseClientProvider>().InitializeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal terhubung ke Supabase: {ex.Message}");
                }
            }

            var shell = _services.GetRequiredService<ShellViewModel>();
            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = shell;
            mainWindow.Show();
            await _services.GetRequiredService<INavigationService>().NavigateTo<LoginViewModel>();
        }
    }
}
