using System.Threading.Tasks;
using DotNetEnv;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Repositories;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.Integration;

[Trait("Category", "Integration")]
public class ReferenceDataIntegrationTests
{
    [IntegrationFact]
    public async Task GetAllAsync_reads_6_activity_types_from_real_supabase_in_order()
    {
        Env.TraversePath().Load();
        var config = AppConfig.FromEnvironment();
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseUrl));
        Assert.False(string.IsNullOrWhiteSpace(config.SupabaseKey));

        var provider = new SupabaseClientProvider(config);
        await provider.InitializeAsync();

        var typesRepo = new SupabaseActivityTypeRepository(provider);
        var categoriesRepo = new SupabaseActivityCategoryRepository(provider);
        var service = new ReferenceDataService(typesRepo, categoriesRepo);

        var result = await service.GetActivityTypesAsync();

        Assert.True(result.IsSuccess, $"Error: {result.Message}");
        var types = result.Value!;
        Assert.Equal(6, types.Count);

        // Verifikasi urutan display order
        Assert.Equal(1, types[0].DisplayOrder);
        Assert.Equal("Jalan Kaki / Bersepeda", types[0].TypeName);
        Assert.Equal("km", types[0].UnitLabel);
        Assert.Equal("Transportasi", types[0].CategoryName);

        Assert.Equal(2, types[1].DisplayOrder);
        Assert.Equal("Transportasi Umum", types[1].TypeName);

        Assert.Equal(3, types[2].DisplayOrder);
        Assert.Equal("Mematikan Lampu Saat Tidak Dipakai", types[2].TypeName);
        Assert.Equal(InputControlType.TimeRange, types[2].InputControlType);
        Assert.Equal("jam", types[2].UnitLabel);

        Assert.Equal(4, types[3].DisplayOrder);
        Assert.Equal("Memilah Sampah Organik/Anorganik", types[3].TypeName);
        Assert.Equal("kali", types[3].UnitLabel);

        Assert.Equal(5, types[4].DisplayOrder);
        Assert.Equal("Membawa Tumbler Sendiri", types[4].TypeName);
        Assert.Equal(InputControlType.Toggle, types[4].InputControlType);

        Assert.Equal(6, types[5].DisplayOrder);
        Assert.Equal("Membawa Kantong Belanja Sendiri", types[5].TypeName);
    }
}
