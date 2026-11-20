using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Core.Domain;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.ViewModels;
using EcoQuest.ViewModels.Items;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

/// <summary>TC-41: photo attachment per activity row (UIR-38), the D-05 preview and the fake end-to-end chain.</summary>
public class ActivityPhotoTests : IDisposable
{
    private const string First = @"C:\fotos\pertama.jpg";
    private const string Second = @"C:\fotos\kedua.png";

    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IPhotoStorageService _photos = Substitute.For<IPhotoStorageService>();
    private readonly NumericItemViewModel _item;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ecoquest-photo-" + Guid.NewGuid().ToString("N"));

    public ActivityPhotoTests()
    {
        _item = (NumericItemViewModel)ActivityInputItemFactory.Create(FakeData.ActivityTypes.Single(t => t.ActivityTypeId == 1));
        _item.ConfigurePhoto(_dialogs, _photos);
        _item.Quantity = 3m;
        _photos.ValidatePhoto(Arg.Any<string>()).Returns(Result.Success());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void Pick(string? path)
    {
        _dialogs.PickImageFile().Returns(path);
        _item.PickPhotoCommand.Execute(null);
    }

    [Fact]
    public void Picking_a_valid_file_attaches_it_and_clears_any_message()
    {
        Pick(First);

        Assert.Equal(First, _item.PhotoSourcePath);
        Assert.True(_item.HasPhoto);
        Assert.False(_item.HasPhotoMessage);
    }

    [Fact]
    public void A_file_that_fails_validation_shows_the_service_message_and_is_not_attached()
    {
        _photos.ValidatePhoto(First).Returns(Result.Failure(ErrorCode.ValidationFailed, "Ukuran foto maksimal 5 MB"));

        Pick(First);

        Assert.Null(_item.PhotoSourcePath);
        Assert.False(_item.HasPhoto);
        Assert.Equal("Ukuran foto maksimal 5 MB", _item.PhotoMessageText);
        Assert.True(_item.HasPhotoMessage);
    }

    [Fact]
    public void A_rejected_replacement_keeps_the_current_photo()
    {
        Pick(First);
        _photos.ValidatePhoto(Second).Returns(Result.Failure(ErrorCode.ValidationFailed, "Foto harus berformat JPG atau PNG"));

        Pick(Second);

        Assert.Equal(First, _item.PhotoSourcePath);
        Assert.Equal("Foto harus berformat JPG atau PNG", _item.PhotoMessageText);
    }

    [Fact]
    public void Cancelling_the_picker_changes_nothing_and_skips_validation()
    {
        Pick(First);
        _photos.ClearReceivedCalls();

        Pick(null);

        Assert.Equal(First, _item.PhotoSourcePath);
        _photos.DidNotReceive().ValidatePhoto(Arg.Any<string>());
    }

    [Fact]
    public void Replacing_swaps_the_photo_and_clears_an_earlier_pick_error()
    {
        _photos.ValidatePhoto(First).Returns(Result.Failure(ErrorCode.ValidationFailed, "gagal"));
        Pick(First);
        Pick(Second);

        Assert.Equal(Second, _item.PhotoSourcePath);
        Assert.False(_item.HasPhotoMessage);
    }

    [Fact]
    public void Removing_clears_the_photo_and_the_pick_error()
    {
        Pick(First);
        _photos.ValidatePhoto(Second).Returns(Result.Failure(ErrorCode.ValidationFailed, "gagal"));
        Pick(Second);

        _item.RemovePhotoCommand.Execute(null);

        Assert.Null(_item.PhotoSourcePath);
        Assert.False(_item.HasPhoto);
        Assert.False(_item.HasPhotoMessage);
    }

    [Fact]
    public void The_pick_error_survives_a_review_validation_pass_and_a_service_error_shows_when_there_is_no_pick_error()
    {
        _photos.ValidatePhoto(First).Returns(Result.Failure(ErrorCode.ValidationFailed, "gagal pilih"));
        Pick(First);

        _item.ApplyIssues([]);
        Assert.Equal("gagal pilih", _item.PhotoMessageText);

        Pick(Second);
        _item.ApplyIssues([new ValidationIssue(1, "Photo", ValidationSeverity.Error, "Ukuran foto maksimal 5 MB")]);
        Assert.Equal("Ukuran foto maksimal 5 MB", _item.PhotoMessageText);
    }

    [Fact]
    public void ToActivityInput_carries_the_note_and_the_photo_path()
    {
        _item.Note = "Jalan kaki ke kampus";
        Pick(First);

        var input = _item.ToActivityInput();

        Assert.Equal("Jalan kaki ke kampus", input.Note);
        Assert.Equal(First, input.PhotoSourcePath);
    }

    [Fact]
    public void Emptying_the_row_hides_the_photo_controls_but_keeps_the_photo_for_when_it_is_filled_again()
    {
        Pick(First);
        _item.Note = "catatan";

        _item.Quantity = 0m;
        Assert.False(_item.IsFilled);
        Assert.Equal(First, _item.PhotoSourcePath);
        Assert.Equal("catatan", _item.Note);

        _item.Quantity = 2m;
        Assert.True(_item.IsFilled);
        Assert.Equal(First, _item.PhotoSourcePath);
    }

    [Fact]
    public void Preview_opens_the_dialog_with_the_attached_file_and_is_unavailable_without_a_photo()
    {
        Assert.False(_item.PreviewPhotoCommand.CanExecute(null));

        Pick(First);
        Assert.True(_item.PreviewPhotoCommand.CanExecute(null));
        _item.PreviewPhotoCommand.Execute(null);

        _dialogs.Received(1).ShowImagePreview(First);
    }

    [Fact]
    public void Without_ConfigurePhoto_the_pick_command_is_disabled()
    {
        var bare = ActivityInputItemFactory.Create(FakeData.ActivityTypes.Single(t => t.ActivityTypeId == 1));

        Assert.False(bare.PickPhotoCommand.CanExecute(null));
    }

    [Fact]
    public void ImagePreviewDialogViewModel_reports_a_missing_file_so_the_placeholder_shows()
    {
        Directory.CreateDirectory(_tempDir);
        var existing = Path.Combine(_tempDir, "ada.png");
        File.WriteAllBytes(existing, [1]);

        Assert.True(new ImagePreviewDialogViewModel(existing, "Tutup").HasImage);
        Assert.False(new ImagePreviewDialogViewModel(Path.Combine(_tempDir, "hilang.png"), "Tutup").HasImage);

        var closing = new ImagePreviewDialogViewModel(existing, "Tutup");
        closing.CloseCommand.Execute(null);
        Assert.True(closing.Result);
    }

    [Fact]
    public async Task Fake_chain_a_photo_attached_in_the_review_resolves_in_history_after_saving()
    {
        Directory.CreateDirectory(_tempDir);
        var source = Path.Combine(_tempDir, "foto.jpg");
        File.WriteAllBytes(source, [1, 2, 3]);
        var root = Path.Combine(_tempDir, "root");

        var world = new FakeWorld(FakeScenario.Normal);
        var review = new FakeEveningReviewService(world.Context, world.Store, root);
        var items = FakeData.ActivityTypes.Select(t => ActivityInput.Empty(t.ActivityTypeId)).ToList();
        items[0] = items[0] with { Quantity = 3m, Note = "bersepeda", PhotoSourcePath = source };

        var saved = await review.SubmitAsync(new SubmitReviewRequest(items, false));
        Assert.True(saved.IsSuccess);

        var history = await world.History.GetHistoryAsync(new HistoryFilter(null, null, []));
        var line = history.Value!.SelectMany(d => d.Items).Single(i => i.PhotoPath is not null);
        Assert.Equal("bersepeda", line.Note);

        var resolved = new FakePhotoStorageService(root).ResolveAbsolutePath(line.PhotoPath);
        Assert.NotNull(resolved);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(resolved!));
    }
}
