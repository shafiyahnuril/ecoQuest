using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Rpc;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using EcoQuest.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace EcoQuest.Tests.Data;

public class RpcClientTests
{
    private sealed class SampleDto
    {
        [JsonProperty("id")] public int Id { get; set; }
    }

    [Fact]
    public void D_formats_DateOnly_as_invariant_iso_date()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("id-ID");
            Assert.Equal("2026-11-03", RpcClient.D(new DateOnly(2026, 11, 3)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-02-09", "2026-02-09")]
    public void D_nullable_formats_DateOnly_or_returns_null(string? input, string? expected)
    {
        DateOnly? date = input is null ? null : DateOnly.ParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expected, RpcClient.D(date));
    }

    [Fact]
    public void T_formats_TimeOnly_24h_or_returns_null()
    {
        Assert.Null(RpcClient.T(null));
        Assert.Equal("07:05:09", RpcClient.T(new TimeOnly(7, 5, 9)));
        Assert.Equal("21:00:00", RpcClient.T(new TimeOnly(21, 0, 0)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData(" null ")]
    public void Deserialize_returns_default_for_null_or_empty_payloads(string? payload)
    {
        Assert.Null(RpcClient.Deserialize<SampleDto>(payload));
    }

    [Fact]
    public void Deserialize_parses_valid_json_payload()
    {
        var dto = RpcClient.Deserialize<SampleDto>("{\"id\":42}");
        Assert.NotNull(dto);
        Assert.Equal(42, dto.Id);
    }

    [Fact]
    public void Deserialize_parses_json_array()
    {
        var list = RpcClient.Deserialize<List<SampleDto>>("[{\"id\":1},{\"id\":2}]");
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[1].Id);
    }

    [Fact]
    public async Task CallAsync_logs_warning_without_request_payload_on_failure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ecoquest-rpc-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new SupabaseClientProvider(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 19, 14, 0, 0, TimeSpan.Zero));
            var logger = new AppLogger(new AppPaths(tempDir), clock);
            var rpc = new RpcClient(provider, logger);

            var ex = await Assert.ThrowsAsync<DataAccessException>(() =>
                rpc.CallAsync<SampleDto>(
                    RpcNames.GetProfile,
                    new Dictionary<string, object?> { ["p_secret"] = "ShouldNeverAppear" },
                    CancellationToken.None));

            Assert.Equal(ErrorCode.Unknown, ex.Code);
            var logFile = Path.Combine(tempDir, "logs", "ecoquest-20261119.log");
            Assert.True(File.Exists(logFile));

            var content = File.ReadAllText(logFile);
            Assert.Contains("[WARN] app_get_profile Unknown InvalidOperationException", content, StringComparison.Ordinal);
            Assert.DoesNotContain("ShouldNeverAppear", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void BackendRegistration_resolves_IClock_IAppPaths_AppLogger_and_RpcClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        Assert.IsType<SystemClock>(sp.GetRequiredService<IClock>());
        Assert.IsType<AppPaths>(sp.GetRequiredService<IAppPaths>());
        Assert.NotNull(sp.GetRequiredService<AppLogger>());
        Assert.NotNull(sp.GetRequiredService<RpcClient>());
    }
}

