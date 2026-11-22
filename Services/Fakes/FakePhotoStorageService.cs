using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Services;

namespace EcoQuest.Services.Fakes;

/// <summary>Validates a picked file like the real service (.jpg/.jpeg/.png, max 5 MB) and resolves relative paths below <c>%AppData%\EcoQuest</c>.</summary>
public sealed class FakePhotoStorageService(string? root = null) : IPhotoStorageService
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EcoQuest");

    private readonly string _root = Path.GetFullPath(root ?? DefaultRoot);

    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];

    public Result ValidatePhoto(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return Result.Failure(ErrorCode.FileError, FakeMessages.FileError);
        if (!Extensions.Contains(Path.GetExtension(sourcePath).ToLowerInvariant()))
            return Result.Failure(ErrorCode.ValidationFailed, "Foto harus berformat JPG atau PNG");

        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists) return Result.Failure(ErrorCode.FileError, FakeMessages.FileError);
            if (info.Length > MaxBytes) return Result.Failure(ErrorCode.ValidationFailed, "Ukuran foto maksimal 5 MB");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result.Failure(ErrorCode.FileError, FakeMessages.FileError);
        }
        return Result.Success();
    }

    public string? ResolveAbsolutePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;

        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        return full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }
}
