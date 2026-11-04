using EcoQuest.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EcoQuest.Services;

/// <summary>
/// Mendaftarkan implementasi backend asli. Dipanggil composition root (App.xaml.cs, ARC-07)
/// SETELAH fake didaftarkan, sehingga registrasi di sini menimpa fake untuk service yang sudah jadi.
/// </summary>
public static class BackendRegistration
{
    public static IServiceCollection AddEcoQuestBackend(this IServiceCollection s)
    {
        s.AddSingleton<SupabaseClientProvider>();
        return s;
    }
}
