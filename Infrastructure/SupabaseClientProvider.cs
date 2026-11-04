using System;
using System.Threading.Tasks;
using Supabase;

namespace EcoQuest.Infrastructure;

public sealed class SupabaseClientProvider
{
    private readonly Client _client;
    private bool _initialized;

    public SupabaseClientProvider(AppConfig config)
    {
        _client = new Client(config.SupabaseUrl!, config.SupabaseKey!, new SupabaseOptions
        {
            AutoConnectRealtime = false,   // realtime tidak dipakai (ADR-002)
            AutoRefreshToken = false
        });
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _client.InitializeAsync();
        _initialized = true;
    }

    public Client Client => _initialized ? _client
        : throw new InvalidOperationException("Supabase belum diinisialisasi");
}
