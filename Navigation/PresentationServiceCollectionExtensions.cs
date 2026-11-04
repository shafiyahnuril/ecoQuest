using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Navigation;

public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything of the presentation layer (SDD-Frontend 2.5). Registered once in FE-02 for ALL screens so
    /// feature branches only add constructor parameters, never lines here.
    /// </summary>
    public static IServiceCollection AddEcoQuestPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IUiText, ResourceUiText>();
        services.AddSingleton<ToastViewModel>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ecoQuest.MainWindow>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EveningReviewViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<AchievementsViewModel>();
        services.AddTransient<LeaderboardViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ConnectionErrorViewModel>();
        return services;
    }
}
