using System.IO;
using EcoQuest.Services.Fakes;
using Xunit;

namespace EcoQuest.Tests.Fakes;

/// <summary>Tests that touch process environment variables; one collection so they never run in parallel.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
    public const string Name = "EnvironmentVariables";
}

[Collection(EnvironmentVariableCollection.Name)]
public sealed class FakeSettingsTests : IDisposable
{
    private readonly string? _flagBefore = Environment.GetEnvironmentVariable(FakeSettings.UseFakesKey);
    private readonly string? _scenarioBefore = Environment.GetEnvironmentVariable(FakeSettings.ScenarioKey);
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(FakeSettings.UseFakesKey, _flagBefore);
        Environment.SetEnvironmentVariable(FakeSettings.ScenarioKey, _scenarioBefore);
        foreach (var dir in _tempDirs) Directory.Delete(dir, recursive: true);
    }

    private string TempDirWithEnv(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecoquest-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".env"), content);
        _tempDirs.Add(dir);
        return dir;
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("yes", false)]
    public void ParseBool_accepts_only_true_or_1(string? value, bool expected) =>
        Assert.Equal(expected, FakeSettings.ParseBool(value));

    [Theory]
    [InlineData("normal", FakeScenario.Normal)]
    [InlineData("EMPTY", FakeScenario.Empty)]
    [InlineData("errors", FakeScenario.Errors)]
    [InlineData("slow", FakeScenario.Slow)]
    [InlineData("nonsense", FakeScenario.Normal)]
    [InlineData("42", FakeScenario.Normal)]
    [InlineData(null, FakeScenario.Normal)]
    public void ParseScenario_defaults_to_normal(string? value, FakeScenario expected) =>
        Assert.Equal(expected, FakeSettings.ParseScenario(value));

    [Fact]
    public void Process_environment_variables_are_honoured()
    {
        Environment.SetEnvironmentVariable(FakeSettings.UseFakesKey, "true");
        Environment.SetEnvironmentVariable(FakeSettings.ScenarioKey, "errors");

        Assert.True(FakesRegistration.IsEnabled());
        Assert.Equal(FakeScenario.Errors, FakeSettings.ReadScenario());
    }

    [Fact]
    public void Flag_is_off_when_variable_is_false()
    {
        Environment.SetEnvironmentVariable(FakeSettings.UseFakesKey, "false");

        Assert.False(FakesRegistration.IsEnabled());
    }

    [Fact]
    public void DotEnv_reader_returns_only_the_two_fake_keys()
    {
        var dir = TempDirWithEnv("SUPABASE_URL=https://example.invalid\nSUPABASE_KEY=dummy-not-a-secret\n# comment\nECOQUEST_USE_FAKES=\"true\"\nECOQUEST_FAKE_SCENARIO = slow\n");

        Assert.Equal("true", FakeSettings.ReadFromDotEnv(FakeSettings.UseFakesKey, dir));
        Assert.Equal("slow", FakeSettings.ReadFromDotEnv(FakeSettings.ScenarioKey, dir));
        Assert.Throws<ArgumentException>(() => FakeSettings.ReadFromDotEnv("SUPABASE_KEY", dir));
        Assert.Throws<ArgumentException>(() => FakeSettings.ReadFromDotEnv("SUPABASE_URL", dir));
    }

    [Fact]
    public void DotEnv_reader_finds_the_file_in_a_parent_folder_and_returns_null_when_key_is_absent()
    {
        var dir = TempDirWithEnv("SUPABASE_URL=https://example.invalid\n");
        var child = Path.Combine(dir, "bin", "Debug");
        Directory.CreateDirectory(child);

        Assert.Null(FakeSettings.ReadFromDotEnv(FakeSettings.UseFakesKey, child));
    }

    [Fact]
    public void Dotenv_flag_is_used_when_the_process_variable_is_unset()
    {
        var dir = TempDirWithEnv("ECOQUEST_USE_FAKES=true\n");
        var child = Path.Combine(dir, "bin");
        Directory.CreateDirectory(child);

        Assert.True(FakeSettings.ParseBool(FakeSettings.ReadFromDotEnv(FakeSettings.UseFakesKey, child)));
    }
}
