using BCrypt.Net;

namespace EcoQuest.Infrastructure;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    /// Hash palsu untuk menyamakan waktu respons saat pengguna tidak ditemukan (mencegah user enumeration).
    public static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing", WorkFactor);

    public string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            return false;
        }
    }
}
