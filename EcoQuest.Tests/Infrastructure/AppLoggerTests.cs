using System;
using System.IO;
using EcoQuest.Infrastructure;
using EcoQuest.Tests.TestDoubles;
using Xunit;

namespace EcoQuest.Tests.Infrastructure;

public sealed class AppLoggerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ecoquest-log-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Abaikan kegagalan cleanup folder temp test.
        }
    }

    [Fact]
    public void Sanitize_masks_apikey_in_url_and_preserves_other_query_params()
    {
        const string input = "GET https://dataservice.accuweather.com/locations?apikey=ABC123&q=Jogja";

        var sanitized = AppLogger.Sanitize(input);

        Assert.DoesNotContain("ABC123", sanitized, StringComparison.Ordinal);
        Assert.Contains("apikey=***", sanitized, StringComparison.Ordinal);
        Assert.Contains("&q=Jogja", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("api_key=SecretKey99", "SecretKey99", "api_key=***")]
    [InlineData("password=Rahasia123&user=budi", "Rahasia123", "password=***")]
    [InlineData("passwordhash=$2a$11$abcdef", "$2a$11$abcdef", "passwordhash=***")]
    [InlineData("APIKEY=UpperSecret&metric=true", "UpperSecret", "APIKEY=***")]
    [InlineData("key=TokenVal\" next", "TokenVal", "key=***")]
    public void Sanitize_masks_sensitive_key_value_pairs(string input, string secret, string expectedMasked)
    {
        var sanitized = AppLogger.Sanitize(input);

        Assert.DoesNotContain(secret, sanitized, StringComparison.Ordinal);
        Assert.Contains(expectedMasked, sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_returns_empty_for_null_or_empty(string? input)
    {
        Assert.Equal(string.Empty, AppLogger.Sanitize(input));
    }

    [Fact]
    public void Warn_writes_formatted_line_to_daily_log_file_in_temp_dir()
    {
        var paths = new AppPaths(_tempDir);
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 19, 10, 15, 30, 123, TimeSpan.Zero));
        var logger = new AppLogger(paths, clock);

        logger.Warn("app_get_profile", "NetworkError", "HttpRequestException");

        var logFile = Path.Combine(_tempDir, "logs", "ecoquest-20261119.log");
        Assert.True(File.Exists(logFile));

        var content = File.ReadAllText(logFile);
        Assert.Contains("2026-11-19T10:15:30.123Z [WARN] app_get_profile NetworkError HttpRequestException", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_and_Error_sanitize_secrets_and_write_expected_levels()
    {
        var paths = new AppPaths(_tempDir);
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
        var logger = new AppLogger(paths, clock);

        logger.Info("weather", "GET /forecast?apikey=MY_SECRET_KEY&metric=true");
        logger.Error("auth", new InvalidOperationException("Failed with password=PlainText123"));

        var logFile = Path.Combine(_tempDir, "logs", "ecoquest-20261119.log");
        var lines = File.ReadAllLines(logFile);

        Assert.Equal(2, lines.Length);
        Assert.Contains("[INFO] weather - GET /forecast?apikey=***&metric=true", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain("MY_SECRET_KEY", lines[0], StringComparison.Ordinal);
        Assert.Contains("[ERROR] auth InvalidOperationException Failed with password=***", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("PlainText123", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Write_does_not_throw_when_log_directory_cannot_be_created()
    {
        // Buat file pada lokasi DataRoot sehingga Path.Combine(DataRoot, "logs") gagal dibuat sebagai direktori.
        var blockingFile = Path.Combine(Path.GetTempPath(), "ecoquest-block-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockingFile, "block");
        try
        {
            var logger = new AppLogger(new AppPaths(blockingFile), new FakeClock(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero)));
            var ex = Record.Exception(() => logger.Warn("op", "Code", "msg"));
            Assert.Null(ex);
        }
        finally
        {
            if (File.Exists(blockingFile)) File.Delete(blockingFile);
        }
    }

    [Fact]
    public void AppPaths_exposes_expected_subdirectories_under_data_root()
    {
        var paths = new AppPaths(_tempDir);

        Assert.Equal(_tempDir, paths.DataRoot);
        Assert.Equal(Path.Combine(_tempDir, "photos"), paths.PhotosRoot);
        Assert.Equal(Path.Combine(_tempDir, "avatars"), paths.AvatarsRoot);
        Assert.Equal(Path.Combine(_tempDir, "logs"), paths.LogsRoot);
        Assert.Equal(Path.Combine(_tempDir, "locationkeys.json"), paths.LocationKeysFile);
    }
}
