using System;
using System.IO;
using System.Linq;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Infrastructure;

namespace EcoQuest.Services;

/// <summary>
/// Menyimpan dan memvalidasi foto aktivitas serta avatar secara lokal di folder data pengguna (ADR-011).
/// </summary>
public sealed class PhotoStorageService : IPhotoStorageService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    public const long MaxBytes = 5L * 1024 * 1024;

    private readonly IAppPaths _paths;

    public PhotoStorageService(IAppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public Result ValidatePhoto(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return Result.Failure(ErrorCode.FileError, Messages.FileError);

            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return Result.Failure(ErrorCode.ValidationFailed, "Foto harus berformat JPG atau PNG");

            if (new FileInfo(sourcePath).Length > MaxBytes)
                return Result.Failure(ErrorCode.ValidationFailed, "Ukuran foto maksimal 5 MB");

            return Result.Success();
        }
        catch (Exception)
        {
            return Result.Failure(ErrorCode.FileError, Messages.FileError);
        }
    }

    public string CopyLogPhoto(string src, Guid userId, Guid logId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(src);

        var ext = Path.GetExtension(src).ToLowerInvariant();
        var rel = $"photos/{userId}/{logId}{ext}";
        var abs = ToAbsolute(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.Copy(src, abs, overwrite: true);
        return rel;
    }

    public string CopyAvatar(string src, Guid userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(src);

        var ext = Path.GetExtension(src).ToLowerInvariant();
        Directory.CreateDirectory(_paths.AvatarsRoot);
        foreach (var old in Directory.EnumerateFiles(_paths.AvatarsRoot, $"{userId}.*"))
        {
            File.Delete(old);
        }

        var rel = $"avatars/{userId}{ext}";
        File.Copy(src, ToAbsolute(rel), overwrite: true);
        return rel;
    }

    public string? ResolveAbsolutePath(string? relativePath)
    {
        if (!TryToSafeAbsolute(relativePath, out var abs))
            return null;

        return File.Exists(abs) ? abs : null;
    }

    public void TryDelete(string? relativePath)
    {
        try
        {
            if (TryToSafeAbsolute(relativePath, out var abs) && File.Exists(abs))
                File.Delete(abs);
        }
        catch
        {
            // Penghapusan best-effort (kompensasi) tidak boleh melempar exception.
        }
    }

    private bool TryToSafeAbsolute(string? relativePath, out string abs)
    {
        abs = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        try
        {
            var root = Path.GetFullPath(_paths.DataRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(ToAbsolute(relativePath));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return false;

            abs = candidate;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private string ToAbsolute(string rel) =>
        Path.Combine(_paths.DataRoot, rel.Replace('/', Path.DirectorySeparatorChar));
}
