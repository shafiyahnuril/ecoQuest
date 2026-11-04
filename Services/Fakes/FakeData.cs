using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Services.Fakes;

public sealed record FakeLevel(int LevelId, string LevelName, int MinPoints, int? MaxPoints);

public sealed record FakeBadge(int BadgeId, string Name, string Description, CriteriaType CriteriaType, decimal CriteriaValue);

/// <summary>
/// The single place where the fake layer holds reference data. There is no C# source for the seed yet, so these values
/// are copied from <c>docs/reference/database/001_schema_v1_2.sql</c> section 3 (SEED DATA). Keep them in sync with
/// that file; real services read the same rows from the database. Never hardcode them anywhere else.
/// </summary>
public static class FakeData
{
    public static readonly Guid DemoUserId = new("5f0c1a52-6b1e-4a52-9f7c-0d4e2b6a1c01");
    public const string DemoUserName = "dinda";
    public const string DemoEmail = "dinda@mail.ugm.ac.id";
    public const string DemoPassword = "Rahasia123";
    public const string DemoCity = "Yogyakarta";
    /// <summary>Registering this username (any scenario) returns UsernameTaken.</summary>
    public const string TakenUserName = "taken";

    public static readonly IReadOnlyList<ActivityCategoryDto> Categories =
    [
        new(1, "Transportasi", "Perjalanan rendah emisi sebagai pengganti kendaraan pribadi"),
        new(2, "Energi", "Penghematan listrik rumah tangga"),
        new(3, "Sampah/Daur Ulang", "Pengelolaan sampah untuk menekan emisi metana di TPA"),
        new(4, "Konsumsi", "Mengurangi barang sekali pakai")
    ];

    public static readonly IReadOnlyList<ActivityTypeDto> ActivityTypes =
    [
        new(1, "Jalan Kaki / Bersepeda", 1, "Transportasi", InputControlType.Numeric, "km", 0.1500m, 1.0m, 30m, 300m,
            "Diasumsikan menggantikan perjalanan mobil pribadi sejauh jarak yang sama (0,15 kg CO2/km).", 1),
        new(2, "Transportasi Umum", 1, "Transportasi", InputControlType.Numeric, "km", 0.0900m, 1.0m, 150m, 1000m,
            "Selisih emisi mobil pribadi vs transportasi umum per km, nilai indikatif (0,09 kg CO2/km).", 2),
        new(3, "Mematikan Lampu Saat Tidak Dipakai", 2, "Energi", InputControlType.TimeRange, "jam", 0.0130m, 0.8m, 12m, 24m,
            "Asumsi 1 lampu sekitar 15 W dan faktor emisi grid PLN sekitar 0,85 kg CO2/kWh (0,013 kg CO2/jam).", 3),
        new(4, "Memilah Sampah Organik/Anorganik", 3, "Sampah/Daur Ulang", InputControlType.Count, "kali", 0.3000m, 1.2m, 10m, 50m,
            "Estimasi pengurangan emisi metana dari sampah organik yang tidak berakhir di TPA campuran (0,3 kg CO2-eq/kejadian).", 4),
        new(5, "Membawa Tumbler Sendiri", 4, "Konsumsi", InputControlType.Toggle, "kali", 0.1000m, 1.0m, null, 1m,
            "Menghindari satu gelas/botol plastik sekali pakai (0,1 kg CO2, maksimal 1x/hari).", 5),
        new(6, "Membawa Kantong Belanja Sendiri", 4, "Konsumsi", InputControlType.Count, "kali", 0.1000m, 1.2m, 5m, 20m,
            "Estimasi emisi produksi kantong plastik sekali pakai yang dihindari per kunjungan (0,1 kg CO2).", 6)
    ];

    public static readonly IReadOnlyList<FakeLevel> Levels =
    [
        new(1, "Eco Starter", 0, 99),
        new(2, "Eco Explorer", 100, 499),
        new(3, "Eco Warrior", 500, 1499),
        new(4, "Eco Champion", 1500, null)
    ];

    public static readonly IReadOnlyList<FakeBadge> Badges =
    [
        new(1, "Langkah Pertama", "Mencatat aktivitas ramah lingkungan pertama kali.", CriteriaType.ActivityCount, 1),
        new(2, "Pencatat Rajin", "Total 30 aktivitas tercatat.", CriteriaType.ActivityCount, 30),
        new(3, "3 Hari Beruntun", "Mengisi Evening Review 3 hari berturut-turut.", CriteriaType.StreakDays, 3),
        new(4, "7 Hari Refleksi Berturut-turut", "Mengisi Evening Review 7 hari berturut-turut.", CriteriaType.StreakDays, 7),
        new(5, "30 Hari Konsisten", "Mengisi Evening Review 30 hari berturut-turut.", CriteriaType.StreakDays, 30),
        new(6, "1 kg CO2 Pertama", "Akumulasi estimasi CO2 yang dihindari mencapai 1 kg.", CriteriaType.CO2SavedThreshold, 1),
        new(7, "10 kg CO2 Pertama", "Akumulasi estimasi CO2 yang dihindari mencapai 10 kg.", CriteriaType.CO2SavedThreshold, 10),
        new(8, "Eco 500 Poin", "Akumulasi poin indikatif mencapai 500 (tidak dapat ditukar).", CriteriaType.PointsThreshold, 500)
    ];

    /// <summary>Stand-in for Resources/Data/cities.json (ADR-012), which does not exist yet.</summary>
    public static readonly IReadOnlyList<string> Cities =
    [
        "Yogyakarta", "Jakarta", "Bandung", "Surabaya", "Semarang", "Surakarta", "Malang", "Medan", "Makassar", "Denpasar"
    ];
}
