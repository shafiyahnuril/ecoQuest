using System;
using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Infrastructure;
using EcoQuest.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcoQuest.Tests.Services;

public sealed class PhotoStorageServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ecoquest-photo-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _srcDir;
    private readonly PhotoStorageService _sut;

    public PhotoStorageServiceTests()
    {
        _srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(_srcDir);
        _sut = new PhotoStorageService(new AppPaths(Path.Combine(_tempDir, "data")));
    }

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
    public void ValidatePhoto_rejects_gif_extension()
    {
        var file = Path.Combine(_srcDir, "anim.gif");
        File.WriteAllBytes(file, [1, 2, 3]);

        var res = _sut.ValidatePhoto(file);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal("Foto harus berformat JPG atau PNG", res.Message);
    }

    [Fact]
    public void ValidatePhoto_rejects_file_larger_than_5mb()
    {
        var file = Path.Combine(_srcDir, "large.jpg");
        using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
        {
            fs.SetLength(6L * 1024 * 1024);
        }

        var res = _sut.ValidatePhoto(file);

        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, res.Error);
        Assert.Equal("Ukuran foto maksimal 5 MB", res.Message);
    }

    [Fact]
    public void ValidatePhoto_accepts_uppercase_PNG_within_limit()
    {
        var file = Path.Combine(_srcDir, "EVIDENCE.PNG");
        File.WriteAllBytes(file, [0x89, 0x50, 0x4E, 0x47]);

        var res = _sut.ValidatePhoto(file);

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public void ValidatePhoto_returns_FileError_for_missing_or_empty_path()
    {
        var missing = _sut.ValidatePhoto(Path.Combine(_srcDir, "tidak-ada.png"));
        var empty = _sut.ValidatePhoto("   ");

        Assert.False(missing.IsSuccess);
        Assert.Equal(ErrorCode.FileError, missing.Error);
        Assert.Equal(Messages.FileError, missing.Message);

        Assert.False(empty.IsSuccess);
        Assert.Equal(ErrorCode.FileError, empty.Error);
    }

    [Fact]
    public void CopyLogPhoto_copies_to_photos_folder_and_returns_forward_slash_relative_path()
    {
        var src = Path.Combine(_srcDir, "sample.PNG");
        File.WriteAllBytes(src, [10, 20, 30]);
        var userId = Guid.NewGuid();
        var logId = Guid.NewGuid();

        var rel = _sut.CopyLogPhoto(src, userId, logId);

        Assert.Equal($"photos/{userId}/{logId}.png", rel);
        var resolved = _sut.ResolveAbsolutePath(rel);
        Assert.NotNull(resolved);
        Assert.True(File.Exists(resolved));
        Assert.Equal(new byte[] { 10, 20, 30 }, File.ReadAllBytes(resolved!));
    }

    [Fact]
    public void CopyAvatar_replaces_previous_avatar_with_different_extension()
    {
        var userId = Guid.NewGuid();
        var jpg = Path.Combine(_srcDir, "old.jpg");
        var png = Path.Combine(_srcDir, "new.png");
        File.WriteAllBytes(jpg, [1, 2]);
        File.WriteAllBytes(png, [3, 4, 5]);

        var firstRel = _sut.CopyAvatar(jpg, userId);
        var secondRel = _sut.CopyAvatar(png, userId);

        Assert.Equal($"avatars/{userId}.jpg", firstRel);
        Assert.Equal($"avatars/{userId}.png", secondRel);
        Assert.Null(_sut.ResolveAbsolutePath(firstRel));
        Assert.NotNull(_sut.ResolveAbsolutePath(secondRel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("photos/../../x")]
    [InlineData("..\\secret.txt")]
    [InlineData("C:\\Windows\\win.ini")]
    public void ResolveAbsolutePath_rejects_traversal_and_unsafe_paths(string? unsafePath)
    {
        Assert.Null(_sut.ResolveAbsolutePath(unsafePath));
    }

    [Fact]
    public void ResolveAbsolutePath_returns_null_after_file_is_deleted_and_TryDelete_is_safe_on_missing()
    {
        var src = Path.Combine(_srcDir, "temp.jpg");
        File.WriteAllBytes(src, [7, 8, 9]);
        var rel = _sut.CopyLogPhoto(src, Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(_sut.ResolveAbsolutePath(rel));

        _sut.TryDelete(rel);
        Assert.Null(_sut.ResolveAbsolutePath(rel));

        var ex = Record.Exception(() => _sut.TryDelete("photos/tidak-ada/file.jpg"));
        Assert.Null(ex);
    }

    [Fact]
    public void BackendRegistration_resolves_PhotoStorageService_and_IPhotoStorageService_as_same_singleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AppConfig("https://example.supabase.co", "dummy-anon-key", null, false));
        services.AddEcoQuestBackend();

        using var sp = services.BuildServiceProvider();
        var concrete = sp.GetRequiredService<PhotoStorageService>();
        var iface = sp.GetRequiredService<IPhotoStorageService>();

        Assert.Same(concrete, iface);
    }
}
