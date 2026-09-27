using System;
using System.Threading.Tasks;
using Supabase;

namespace ecoQuest.Services
{
    public class SupabaseService
    {
        private static SupabaseService? _instance;
        private readonly Client _client;

        private SupabaseService()
        {
            var url = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? throw new ArgumentNullException("SUPABASE_URL is missing in environment variables.");
            var key = Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? throw new ArgumentNullException("SUPABASE_KEY is missing in environment variables.");

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true
            };

            _client = new Client(url, key, options);
        }

        public static SupabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SupabaseService();
                }
                return _instance;
            }
        }

        public async Task InitializeAsync()
        {
            await _client.InitializeAsync();
        }

        public Client Client => _client;
    }
}
