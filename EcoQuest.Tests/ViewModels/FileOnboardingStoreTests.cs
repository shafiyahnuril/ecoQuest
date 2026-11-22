using System.IO;
using EcoQuest.Navigation;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>Uses a temporary folder only; the real %AppData%\EcoQuest is never touched.</summary>
public class FileOnboardingStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ecoquest-onboarding-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "onboarding.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_means_not_completed()
    {
        Assert.False(new FileOnboardingStore(FilePath).HasCompleted(Guid.NewGuid()));
    }

    [Fact]
    public void Marked_user_is_remembered_by_a_new_store_instance()
    {
        var user = Guid.NewGuid();
        new FileOnboardingStore(FilePath).MarkCompleted(user);

        Assert.True(new FileOnboardingStore(FilePath).HasCompleted(user));
    }

    [Fact]
    public void Users_are_isolated_and_marking_twice_keeps_both()
    {
        var store = new FileOnboardingStore(FilePath);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        store.MarkCompleted(first);
        Assert.False(store.HasCompleted(second));

        store.MarkCompleted(second);
        store.MarkCompleted(second);

        Assert.True(store.HasCompleted(first));
        Assert.True(store.HasCompleted(second));
        Assert.False(store.HasCompleted(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json {{{")]
    [InlineData("{\"completed\": [\"not-a-guid\"]}")]
    [InlineData("[1,2,3]")]
    public void Corrupt_file_means_not_completed_and_is_repaired_on_the_next_write(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, content);
        var store = new FileOnboardingStore(FilePath);
        var user = Guid.NewGuid();

        Assert.False(store.HasCompleted(user));

        store.MarkCompleted(user);

        Assert.True(new FileOnboardingStore(FilePath).HasCompleted(user));
    }

    [Fact]
    public void Write_is_atomic_and_leaves_no_temporary_file()
    {
        var store = new FileOnboardingStore(FilePath);

        store.MarkCompleted(Guid.NewGuid());
        store.MarkCompleted(Guid.NewGuid());

        Assert.Equal([FilePath], Directory.GetFiles(_dir));
    }

    [Fact]
    public void An_unwritable_location_never_throws()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var store = new FileOnboardingStore(Path.Combine(blocker, "onboarding.json"));
        var user = Guid.NewGuid();

        store.MarkCompleted(user);

        Assert.False(store.HasCompleted(user));
    }
}
