using EcoQuest.Core.Common;

namespace EcoQuest.Services.Fakes;

/// <summary>Shared scenario, clock, latency and "fail once" gates for every fake service (UIR-81, UIR-83).</summary>
public sealed class FakeContext
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delayer;
    private readonly Random _delayRandom = new();
    private readonly HashSet<string> _failedKeys = new();
    private readonly object _lock = new();

    public FakeContext(FakeScenario scenario, IClock clock, Func<TimeSpan, CancellationToken, Task>? delayer = null)
    {
        Scenario = scenario;
        Clock = clock;
        _delayer = delayer ?? Task.Delay;   // tests inject a recorder so they never wait
    }

    public FakeScenario Scenario { get; }
    public IClock Clock { get; }

    /// <summary>3 s in Slow, otherwise a random 300-800 ms.</summary>
    public TimeSpan NextDelay()
    {
        if (Scenario == FakeScenario.Slow) return TimeSpan.FromSeconds(3);
        lock (_lock) return TimeSpan.FromMilliseconds(_delayRandom.Next(300, 801));
    }

    public Task DelayAsync(CancellationToken ct) => _delayer(NextDelay(), ct);

    /// <summary>
    /// True exactly once per <paramref name="key"/>, and only in the Errors scenario. The first load fails with
    /// NetworkError; "Coba lagi" then succeeds, which exercises the retry flow.
    /// </summary>
    public bool FailOnce(string key)
    {
        if (Scenario != FakeScenario.Errors) return false;
        lock (_lock) return _failedKeys.Add(key);
    }

    public Result<T> NetworkFailure<T>() => Result<T>.Failure(ErrorCode.NetworkError, FakeMessages.Network);
}

/// <summary>Standard error texts from Interface-Contracts.md section 7.</summary>
public static class FakeMessages
{
    public const string Network = "Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi";
    public const string UsernameTaken = "Username sudah digunakan";
    public const string EmailTaken = "Email sudah terdaftar";
    public const string InvalidCredentials = "Kombinasi username/email dan password salah";
    public const string WrongOldPassword = "Password lama tidak sesuai";
    public const string ReviewAlreadySubmitted = "Refleksi hari ini sudah tersimpan dan tidak dapat diubah";
    public const string WarningsNotAcknowledged = "Ada nilai yang tidak biasa. Mohon konfirmasi terlebih dahulu";
    public const string ValidationFailed = "Periksa kembali isian yang ditandai";
    public const string FileError = "Berkas tidak dapat dibaca atau disimpan";
    public const string NotAuthenticated = "Sesi tidak ditemukan. Silakan masuk kembali";
}
