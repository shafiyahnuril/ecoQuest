
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DotNetEnv;
using EcoQuest.Core.Common;
using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.Services;
using EcoQuest.Services.Fakes;
using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ecoQuest
{
    /// <summary>
    /// Composition root aplikasi EcoQuest.
    /// Mengatur konfigurasi, DI, culture, logging,
    /// exception handling, dan navigasi awal.
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _services;
        private AppLogger? _logger;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Gunakan format Indonesia untuk seluruh aplikasi.
            var culture = CultureInfo.GetCultureInfo("id-ID");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Tangani exception global sejak awal startup.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                // Cari .env dari direktori aplikasi dan parent-nya.
                // Mode fake tetap dapat berjalan tanpa .env.
                Env.TraversePath().Load();

                var config = AppConfig.FromEnvironment();
                var missingKeys = config.MissingRequiredKeys();

                var services = new ServiceCollection()
                    .AddEcoQuestPresentation();

                services.AddSingleton(config);

                // Infrastruktur dasar harus tersedia pada
                // mode fake maupun mode real.
                services.TryAddSingleton<IClock, SystemClock>();
                services.TryAddSingleton<IAppPaths, AppPaths>();
                services.TryAddSingleton<AppLogger>();

                // Pertahankan registrasi layanan yang ada.
                services.AddEcoQuestFakes();

                var useReal = !config.UseFakes && missingKeys.Count == 0;

                if (useReal)
                {
                    services.AddEcoQuestBackend();
                }

                _services = services.BuildServiceProvider();
                _logger = _services.GetRequiredService<AppLogger>();

                _logger.Info(
                    "Startup",
                    config.UseFakes
                        ? "Memulai aplikasi dalam mode fake."
                        : "Memulai aplikasi dalam mode real."
                );

                // Siapkan shell dan jendela utama.
                var shell = _services.GetRequiredService<ShellViewModel>();
                var mainWindow = _services.GetRequiredService<MainWindow>();

                mainWindow.DataContext = shell;
                MainWindow = mainWindow;
                mainWindow.Show();

                var navigation = _services.GetRequiredService<INavigationService>();

                // Konfigurasi real tidak lengkap:
                // jangan diam-diam beralih ke fake.
                if (!config.UseFakes && missingKeys.Count > 0)
                {
                    _logger.Warn(
                        "Startup",
                        "CONFIG_MISSING",
                        "Konfigurasi layanan wajib belum lengkap."
                    );

                    await navigation.NavigateTo<ConnectionErrorViewModel>();
                    return;
                }

                if (useReal)
                {
                    try
                    {
                        await _services
                            .GetRequiredService<SupabaseClientProvider>()
                            .InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        // Jangan tampilkan pesan exception mentah
                        // karena mungkin memuat informasi sensitif.
                        _logger.Error(
                            "SupabaseInitialization",
                            ex
                        );

                        await navigation.NavigateTo<ConnectionErrorViewModel>();
                        return;
                    }
                }

                // Startup berhasil: tampilkan Login.
                await navigation.NavigateTo<LoginViewModel>();
            }
            catch (Exception ex)
            {
                LogSafely("Startup", ex);

                MessageBox.Show(
                    "Aplikasi gagal dimulai. Silakan periksa konfigurasi atau hubungi pengelola.",
                    "EcoQuest - Kesalahan Startup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                Shutdown(1);
            }
        }

        /// <summary>
        /// Menangani exception pada thread UI WPF.
        /// </summary>
        private void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            LogSafely("DispatcherUnhandledException", e.Exception);

            MessageBox.Show(
                "Terjadi kesalahan pada aplikasi. Silakan coba kembali.",
                "EcoQuest - Kesalahan",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            e.Handled = true;
        }

        /// <summary>
        /// Menangani exception yang tidak tertangani
        /// pada domain aplikasi.
        /// </summary>
        private void OnDomainUnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogSafely("AppDomainUnhandledException", ex);
            }
        }

        /// <summary>
        /// Menangani exception Task yang tidak diamati.
        /// </summary>
        private void OnUnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            LogSafely("TaskSchedulerUnobservedException", e.Exception);
            e.SetObserved();
        }

        /// <summary>
        /// Logging tidak boleh menyebabkan exception tambahan.
        /// </summary>
        private void LogSafely(string operation, Exception exception)
        {
            try
            {
                // Hanya log jenis exception jika logger belum tersedia.
                // Hindari mencetak detail sensitif ke UI.
                _logger?.Error(operation, exception);
            }
            catch
            {
                // Abaikan kegagalan logging.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

            _services?.Dispose();

            base.OnExit(e);
        }
    }
}
