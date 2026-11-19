using EcoQuest.Infrastructure;
using Xunit;

namespace EcoQuest.Tests.Infrastructure;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_twice_gives_different_results()
    {
        var a = _sut.Hash("Rahasia123");
        var b = _sut.Hash("Rahasia123");

        Assert.NotEqual(a, b);
        Assert.StartsWith("$2", a);
        Assert.StartsWith("$2", b);
    }

    [Fact]
    public void Verify_correct_and_wrong_password()
    {
        var hash = _sut.Hash("Rahasia123");

        Assert.True(_sut.Verify("Rahasia123", hash));
        Assert.False(_sut.Verify("SalahBanget1", hash));
    }

    [Fact]
    public void Verify_invalid_hash_returns_false_without_throwing()
        => Assert.False(_sut.Verify("x", "bukan-hash"));

    [Fact]
    public void DummyHash_starts_with_work_factor_11()
        => Assert.StartsWith("$2a$11$", BCryptPasswordHasher.DummyHash);
}
