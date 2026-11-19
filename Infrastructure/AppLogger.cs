using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using EcoQuest.Core.Common;

namespace EcoQuest.Infrastructure;

/// <summary>
/// Logger file harian yang menyaring nilai sensitif (password, hash, API key) sebelum menulis ke disk.
/// </summary>
public sealed class AppLogger
{
    private static readonly Regex Secret = new(
        @"(apikey|api_key|password|passwordhash|key)=([^&\s""]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAppPaths _paths;
    private readonly IClock _clock;
    private readonly object _gate = new();

    public AppLogger(IAppPaths paths, IClock clock)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Info(string op, string msg) => Write("INFO", op, "-", msg);

    public void Warn(string op, string code, string msg) => Write("WARN", op, code, msg);

    public void Error(string op, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        Write("ERROR", op, ex.GetType().Name, ex.Message);
    }

    public static string Sanitize(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : Secret.Replace(s, m => $"{m.Groups[1].Value}=***");

    private void Write(string level, string op, string code, string msg)
    {
        try
        {
            var now = _clock.UtcNow;
            var dir = Path.Combine(_paths.DataRoot, "logs");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"ecoquest-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
            var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var line = $"{timestamp} [{level}] {op} {code} {Sanitize(msg)}";

            lock (_gate)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging tidak boleh membuat aplikasi crash.
        }
    }
}
