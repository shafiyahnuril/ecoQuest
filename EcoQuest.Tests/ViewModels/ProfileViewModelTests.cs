using System.ComponentModel.DataAnnotations;
using System.IO;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.Services.Fakes;
using EcoQuest.Tests.Fakes;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public sealed class ProfileViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ecoquest-profile-" + Guid.NewGuid().ToString("N"));
    private readonly FakeWorld _world;
    private readonly FakeProfileService _profile;
    private readonly FakePhotoStorageService _photos;
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IUiText _text = Substitute.For<IUiText>();

    public ProfileViewModelTests()
    {
        Directory.CreateDirectory(_root);
        _world = new FakeWorld(FakeScenario.Normal);
        _profile = new FakeProfileService(_world.Context, _world.Store, _world.Session, _root);
        _photos = new FakePhotoStorageService(_root);
        _text.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());
        _dialogs.ConfirmAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(true);
        LogIn(_world);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // The Errors scenario fails the first login once, so a second attempt is allowed.
    private static void LogIn(FakeWorld world)
    {
        var request = new LoginRequest(FakeData.DemoUserName, FakeData.DemoPassword);
        if (!world.Auth.LoginAsync(request).GetAwaiter().GetResult().IsSuccess)
        {
            Assert.True(world.Auth.LoginAsync(request).GetAwaiter().GetResult().IsSuccess);
        }
    }

    private ProfileViewModel Create(IProfileService? profile = null, FakeWorld? world = null) =>
        new(profile ?? _profile, _photos, _dialogs, _text, (world ?? _world).Clock);

    private async Task<ProfileViewModel> LoadedAsync(IProfileService? profile = null)
    {
        var vm = Create(profile);
        await vm.OnNavigatedToAsync(null);
        return vm;
    }

    private ShellViewModel NewShell() =>
        new(Substitute.For<INavigationService>(), _dialogs, _text, new ToastViewModel(), _world.Session, _world.Auth, _world.Review, _photos);

    private string NewImage(string name = "foto.png", int bytes = 64)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private string[] AvatarFiles() =>
        Directory.Exists(Path.Combine(_root, "avatars")) ? Directory.GetFiles(Path.Combine(_root, "avatars")) : [];

    private static string? ErrorOf(ProfileViewModel vm, string field) =>
        vm.GetErrors(field).OfType<ValidationResult>().Select(r => r.ErrorMessage).FirstOrDefault();

    private static string Other(string city) => FakeData.Cities.First(c => c != city);

    // ---------- loading ----------

    [Fact]
    public async Task Load_fills_the_account_and_ends_Loaded()
    {
        var vm = await LoadedAsync();

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Equal(FakeData.DemoUserName, vm.UserName);
        Assert.Equal(FakeData.DemoEmail, vm.Email);
        Assert.Equal(FakeData.DemoCity, vm.City);
        Assert.Equal("D", vm.AvatarInitial);
        Assert.False(vm.HasAvatar);
        Assert.Equal(FakeData.Cities, vm.Cities);
        Assert.Equal("Minggu, 25 Oktober 2026", vm.TodayText);
    }

    [Fact]
    public async Task Load_failure_shows_the_error_state_and_Retry_recovers()
    {
        var world = new FakeWorld(FakeScenario.Errors);
        LogIn(world);
        var vm = Create(new FakeProfileService(world.Context, world.Store, world.Session, _root), world);

        await vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal(FakeMessages.Network, vm.ErrorMessage);

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(FakeData.DemoUserName, vm.UserName);
    }

    [Fact]
    public async Task State_is_Loading_while_the_profile_is_being_read()
    {
        var gate = new TaskCompletionSource<Result<UserProfileDto>>();
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = Create(service);

        var load = vm.OnNavigatedToAsync(null);
        Assert.Equal(LoadState.Loading, vm.State);

        gate.SetResult((await _profile.GetProfileAsync()));
        await load;
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    // ---------- city ----------

    [Fact]
    public async Task Saving_the_city_toasts_and_the_sidebar_follows_the_session()
    {
        var vm = await LoadedAsync();
        var shell = NewShell();
        var city = Other(vm.City);

        vm.City = city;
        await vm.SaveProfileCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowToast("Profile.Saved");
        Assert.Equal(city, shell.City);
        Assert.Equal(city, _world.Session.Current!.City);
    }

    [Fact]
    public async Task Unknown_city_error_lands_on_the_city_field_and_editing_it_clears_the_error()
    {
        var vm = await LoadedAsync();

        vm.City = "Atlantis";
        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(ErrorOf(vm, nameof(vm.City))));
        Assert.Null(vm.ErrorMessage);
        _dialogs.DidNotReceive().ShowToast(Arg.Any<string>());

        vm.City = FakeData.DemoCity;
        Assert.Null(ErrorOf(vm, nameof(vm.City)));
    }

    [Fact]
    public async Task Network_failure_while_saving_goes_to_the_banner_and_keeps_the_form()
    {
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(await _profile.GetProfileAsync());
        service.UpdateProfileAsync(Arg.Any<UpdateProfileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserProfileDto>.Failure(ErrorCode.NetworkError, FakeMessages.Network));
        var vm = await LoadedAsync(service);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal(FakeMessages.Network, vm.ErrorMessage);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.False(vm.HasErrors);
    }

    // ---------- avatar ----------

    [Fact]
    public async Task Picking_a_valid_photo_previews_it_before_saving()
    {
        var vm = await LoadedAsync();
        var path = NewImage();
        _dialogs.PickImageFile().Returns(path);

        vm.PickAvatarCommand.Execute(null);

        Assert.Equal(path, vm.AvatarImagePath);
        Assert.True(vm.HasAvatar);
        Assert.Null(vm.AvatarError);
        Assert.Empty(AvatarFiles());
    }

    [Fact]
    public async Task Cancelling_the_picker_changes_nothing()
    {
        var vm = await LoadedAsync();
        _dialogs.PickImageFile().Returns((string?)null);

        vm.PickAvatarCommand.Execute(null);

        Assert.False(vm.HasAvatar);
        Assert.Null(vm.AvatarError);
    }

    [Theory]
    [InlineData("dokumen.txt", 10)]
    [InlineData("besar.png", 5 * 1024 * 1024 + 1)]
    public async Task An_invalid_photo_shows_the_service_message_near_the_avatar(string name, int bytes)
    {
        var vm = await LoadedAsync();
        var path = NewImage(name, bytes);
        _dialogs.PickImageFile().Returns(path);

        vm.PickAvatarCommand.Execute(null);

        Assert.Equal(_photos.ValidatePhoto(path).Message, vm.AvatarError);
        Assert.False(vm.HasAvatar);
    }

    [Fact]
    public async Task Saving_a_photo_stores_it_and_the_profile_and_sidebar_show_the_stored_copy()
    {
        var vm = await LoadedAsync();
        var shell = NewShell();
        _dialogs.PickImageFile().Returns(NewImage());
        vm.PickAvatarCommand.Execute(null);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        var stored = Assert.Single(AvatarFiles());
        Assert.Equal(stored, vm.AvatarImagePath, ignoreCase: true);
        Assert.Equal(stored, shell.AvatarImagePath, ignoreCase: true);
        Assert.Null(vm.AvatarError);
    }

    [Fact]
    public async Task Replacing_the_photo_deletes_the_previous_stored_copy()
    {
        var vm = await LoadedAsync();
        _dialogs.PickImageFile().Returns(NewImage("a.png"));
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);
        var first = Assert.Single(AvatarFiles());

        _dialogs.PickImageFile().Returns(NewImage("b.jpg"));
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);

        var second = Assert.Single(AvatarFiles());
        Assert.NotEqual(first, second);
        Assert.False(File.Exists(first));
    }

    [Fact]
    public async Task Removing_the_photo_asks_first_and_saving_deletes_the_stored_copy()
    {
        var vm = await LoadedAsync();
        var shell = NewShell();
        _dialogs.PickImageFile().Returns(NewImage());
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);

        await vm.RemoveAvatarCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync("Profile.RemovePhotoTitle", "Profile.RemovePhotoMessage", "Profile.RemovePhotoConfirm", null, DialogIcon.Warning);
        Assert.False(vm.HasAvatar);
        Assert.Single(AvatarFiles());

        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.Empty(AvatarFiles());
        Assert.False(vm.HasAvatar);
        Assert.Null(shell.AvatarImagePath);
        Assert.Equal("D", vm.AvatarInitial);
    }

    [Fact]
    public async Task The_photo_survives_logging_out_and_in_again_with_the_same_stored_file()
    {
        var vm = await LoadedAsync();
        _dialogs.PickImageFile().Returns(NewImage());
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);
        var saved = _world.Session.Current!.AvatarPath;
        Assert.NotNull(saved);

        _world.Auth.Logout();
        Assert.Null(_world.Session.Current);
        var login = await _world.Auth.LoginAsync(new LoginRequest(FakeData.DemoUserName, FakeData.DemoPassword));

        Assert.Equal(saved, login.Value!.AvatarPath);
        Assert.Equal(saved, _world.Session.Current!.AvatarPath);
        Assert.True(File.Exists(_photos.ResolveAbsolutePath(saved)));
        var reopened = await LoadedAsync();
        Assert.True(reopened.HasAvatar);
        Assert.Equal(_photos.ResolveAbsolutePath(saved), reopened.AvatarImagePath);
    }

    [Fact]
    public async Task Removing_the_photo_is_also_forgotten_at_the_next_login()
    {
        var vm = await LoadedAsync();
        _dialogs.PickImageFile().Returns(NewImage());
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);
        await vm.RemoveAvatarCommand.ExecuteAsync(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);

        _world.Auth.Logout();
        var login = await _world.Auth.LoginAsync(new LoginRequest(FakeData.DemoUserName, FakeData.DemoPassword));

        Assert.Null(login.Value!.AvatarPath);
    }

    [Fact]
    public async Task Declining_the_confirmation_keeps_the_photo()
    {
        var vm = await LoadedAsync();
        _dialogs.PickImageFile().Returns(NewImage());
        vm.PickAvatarCommand.Execute(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(false);

        await vm.RemoveAvatarCommand.ExecuteAsync(null);
        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(vm.HasAvatar);
        Assert.Single(AvatarFiles());
    }

    [Fact]
    public async Task Remove_is_unavailable_without_a_photo()
    {
        var vm = await LoadedAsync();

        Assert.False(vm.RemoveAvatarCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_file_error_from_the_service_is_shown_near_the_avatar_not_in_the_banner()
    {
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(await _profile.GetProfileAsync());
        service.UpdateProfileAsync(Arg.Any<UpdateProfileRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserProfileDto>.Failure(ErrorCode.FileError, FakeMessages.FileError));
        var vm = await LoadedAsync(service);

        await vm.SaveProfileCommand.ExecuteAsync(null);

        Assert.Equal(FakeMessages.FileError, vm.AvatarError);
        Assert.Null(vm.ErrorMessage);
    }

    // ---------- password ----------

    [Fact]
    public async Task A_wrong_old_password_is_shown_on_the_old_password_field()
    {
        var vm = await LoadedAsync();
        vm.OldPassword = "salahsalah1";
        vm.NewPassword = "Baru12345";
        vm.ConfirmNewPassword = "Baru12345";

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Equal("Profile.WrongOldPassword", ErrorOf(vm, nameof(vm.OldPassword)));
        Assert.Equal("Profile.WrongOldPassword", vm.OldPasswordError);
        Assert.NotEqual(FakeMessages.WrongOldPassword, vm.OldPasswordError);
        Assert.Null(ErrorOf(vm, nameof(vm.NewPassword)));
        Assert.Null(vm.ErrorMessage);
        _dialogs.DidNotReceive().ShowToast(Arg.Any<string>());

        vm.OldPassword = "salahsalah12";
        Assert.Null(vm.OldPasswordError);
    }

    [Fact]
    public async Task A_wrong_old_password_without_a_field_key_still_lands_on_the_old_password_field()
    {
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(await _profile.GetProfileAsync());
        service.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCode.WrongOldPassword, FakeMessages.WrongOldPassword));
        var vm = await LoadedAsync(service);

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Equal("Profile.WrongOldPassword", ErrorOf(vm, nameof(vm.OldPassword)));
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Server_format_errors_land_on_the_new_and_confirm_fields_and_editing_clears_only_that_field()
    {
        var vm = await LoadedAsync();
        vm.OldPassword = FakeData.DemoPassword;
        vm.NewPassword = "pendek";
        vm.ConfirmNewPassword = "beda";

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(ErrorOf(vm, nameof(vm.NewPassword))));
        Assert.False(string.IsNullOrEmpty(ErrorOf(vm, nameof(vm.ConfirmNewPassword))));
        Assert.Null(ErrorOf(vm, nameof(vm.OldPassword)));

        vm.NewPassword = "Baru12345";
        Assert.Null(ErrorOf(vm, nameof(vm.NewPassword)));
        Assert.False(string.IsNullOrEmpty(ErrorOf(vm, nameof(vm.ConfirmNewPassword))));
    }

    [Fact]
    public async Task A_successful_change_toasts_clears_the_three_fields_and_errors()
    {
        var vm = await LoadedAsync();
        vm.OldPassword = FakeData.DemoPassword;
        vm.NewPassword = "Baru12345";
        vm.ConfirmNewPassword = "Baru12345";

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowToast("Profile.PasswordChanged");
        Assert.Equal("", vm.OldPassword);
        Assert.Equal("", vm.NewPassword);
        Assert.Equal("", vm.ConfirmNewPassword);
        Assert.False(vm.HasErrors);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task A_network_failure_while_changing_the_password_goes_to_the_banner()
    {
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(await _profile.GetProfileAsync());
        service.ChangePasswordAsync(Arg.Any<ChangePasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCode.NetworkError, FakeMessages.Network));
        var vm = await LoadedAsync(service);

        await vm.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Equal(FakeMessages.Network, vm.ErrorMessage);
        Assert.False(vm.HasErrors);
    }

    // ---------- double submit ----------

    [Fact]
    public async Task A_second_save_while_the_first_runs_does_not_reach_the_service()
    {
        var gate = new TaskCompletionSource<Result<UserProfileDto>>();
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        var profile = await _profile.GetProfileAsync();
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(profile);
        service.UpdateProfileAsync(Arg.Any<UpdateProfileRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = await LoadedAsync(service);

        var first = vm.SaveProfileCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.SaveProfileCommand.CanExecute(null));
        Assert.False(vm.ChangePasswordCommand.CanExecute(null));
        await vm.SaveProfileCommand.ExecuteAsync(null);

        gate.SetResult(profile);
        await first;

        await service.Received(1).UpdateProfileAsync(Arg.Any<UpdateProfileRequest>(), Arg.Any<CancellationToken>());
        Assert.False(vm.IsBusy);
        Assert.True(vm.SaveProfileCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_password_change_cannot_start_while_a_save_runs()
    {
        var gate = new TaskCompletionSource<Result<UserProfileDto>>();
        var service = Substitute.For<IProfileService>();
        service.GetSupportedCities().Returns(FakeData.Cities);
        var profile = await _profile.GetProfileAsync();
        service.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(profile);
        service.UpdateProfileAsync(Arg.Any<UpdateProfileRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);
        var vm = await LoadedAsync(service);

        var save = vm.SaveProfileCommand.ExecuteAsync(null);
        await vm.ChangePasswordCommand.ExecuteAsync(null);
        gate.SetResult(profile);
        await save;

        await service.DidNotReceive().ChangePasswordAsync(Arg.Any<ChangePasswordRequest>(), Arg.Any<CancellationToken>());
    }
}
