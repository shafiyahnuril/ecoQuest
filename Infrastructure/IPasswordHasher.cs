namespace EcoQuest.Infrastructure;

public interface IPasswordHasher
{
    /// Menghasilkan hash BCrypt (work factor 11) dengan salt acak.
    string Hash(string password);

    /// Memverifikasi password terhadap hash. Hash yang rusak mengembalikan false, tidak melempar.
    bool Verify(string password, string hash);
}
