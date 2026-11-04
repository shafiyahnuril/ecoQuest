using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Repositories;
using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Services;

public static class BackendRegistration
{
    public static IServiceCollection AddEcoQuestBackend(this IServiceCollection s)
    {
        s.AddSingleton<SupabaseClientProvider>();
        s.AddSingleton<SupabaseActivityTypeRepository>();
        s.AddSingleton<IActivityTypeRepository>(sp =>
            new CachedActivityTypeRepository(sp.GetRequiredService<SupabaseActivityTypeRepository>()));
        s.AddSingleton<IActivityCategoryRepository, SupabaseActivityCategoryRepository>();
        s.AddSingleton<IReferenceDataService, ReferenceDataService>();
        return s;
    }
}
