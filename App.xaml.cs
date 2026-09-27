using System.Configuration;
using System.Data;
using System.Windows;
using DotNetEnv;
using ecoQuest.Services;

namespace ecoQuest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load environment variables dari file .env
            Env.Load();

            // Inisialisasi Supabase
            try 
            {
                await SupabaseService.Instance.InitializeAsync();
                
                // --- TEST SEED DATA KE SUPABASE ---
                var testUser = new ecoQuest.Models.UserModel
                {
                    UserName = "BagasTester " + new Random().Next(1000),
                    Email = "bagas" + new Random().Next(1000) + "@tester.com",
                    PasswordHash = "rahasia123",
                    City = "Yogyakarta",
                    CreatedAt = DateTime.UtcNow
                };

                var insertResult = await SupabaseService.Instance.Client.From<ecoQuest.Models.UserModel>().Insert(testUser);
                var inserted = insertResult.Models.FirstOrDefault();

                if (inserted != null)
                {
                    MessageBox.Show($"Koneksi Valid! Data Dummy (User: {inserted.UserName}) berhasil masuk ke Supabase.", "Sukses Seed Data");
                }
                // ----------------------------------
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal terhubung ke Supabase: {ex.Message}");
            }
        }
    }
}

