using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class ConnectionErrorViewModelTests
{
    private const string SecretUrl = "https://secret-project.example.test";
    private const string SecretKey = "secret-anon-key-value";
    private const string SecretWeatherKey = "secret-weather-token";

    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IUiText _text = Substitute.For<IUiText>();

    public ConnectionErrorViewModelTests()
    {
        _text.Get("Offline.Title").Returns("Tidak dapat terhubung ke server");
        _text.Get("Offline.Body").Returns("Periksa koneksi internet lalu coba lagi.");
        _text.Get("Offline.ConfigBody").Returns("Konfigurasi belum lengkap: {0} belum diisi di file .env.");
        _text.Get("Offline.ConfigHint").Returns("Isi file .env lalu jalankan ulang aplikasi.");
    }

    private ConnectionErrorViewModel Create(AppConfig config) => new(config, _nav, _text);

    [Theory]
    [InlineData(true, null, null)]
    [InlineData(true, SecretUrl, SecretKey)]
    [InlineData(false, SecretUrl, SecretKey)]
    public void Network_variant_when_fakes_are_on_or_required_keys_are_complete(bool useFakes, string? url, string? key)
    {
        var vm = Create(new AppConfig(url, key, null, useFakes));

        Assert.False(vm.IsConfigurationVariant);
        Assert.Equal("Tidak dapat terhubung ke server", vm.Title);
        Assert.Equal("Periksa koneksi internet lalu coba lagi.", vm.Message);
        Assert.Null(vm.Hint);
        Assert.True(vm.RetryCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(null, SecretKey, "SUPABASE_URL")]
    [InlineData(SecretUrl, " ", "SUPABASE_KEY")]
    [InlineData("", null, "SUPABASE_URL, SUPABASE_KEY")]
    public void Configuration_variant_lists_the_missing_key_names(string? url, string? key, string expectedNames)
    {
        var vm = Create(new AppConfig(url, key, null, UseFakes: false));

        Assert.True(vm.IsConfigurationVariant);
        Assert.Equal("Tidak dapat terhubung ke server", vm.Title);
        Assert.Equal($"Konfigurasi belum lengkap: {expectedNames} belum diisi di file .env.", vm.Message);
        Assert.Equal("Isi file .env lalu jalankan ulang aplikasi.", vm.Hint);
    }

    [Fact]
    public void Configuration_message_never_contains_a_configuration_value()
    {
        var vm = Create(new AppConfig(SecretUrl, null, SecretWeatherKey, UseFakes: false));
        var shownText = vm.Title + vm.Message + vm.Hint;

        Assert.DoesNotContain(SecretUrl, shownText);
        Assert.DoesNotContain(SecretWeatherKey, shownText);
        Assert.DoesNotContain(SecretKey, shownText);
    }

    [Fact]
    public void Configuration_variant_has_no_retry()
    {
        var vm = Create(new AppConfig(null, null, null, UseFakes: false));

        Assert.False(vm.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Retry_goes_back_when_there_is_history()
    {
        _nav.CanGoBack.Returns(true);
        var vm = Create(new AppConfig(null, null, null, UseFakes: true));

        await vm.RetryCommand.ExecuteAsync(null);

        await _nav.Received(1).GoBackAsync();
        await _nav.DidNotReceive().NavigateTo<LoginViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task Retry_goes_to_login_without_history()
    {
        _nav.CanGoBack.Returns(false);
        var vm = Create(new AppConfig(null, null, null, UseFakes: true));

        await vm.RetryCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<LoginViewModel>(Arg.Any<object?>());
        await _nav.DidNotReceive().GoBackAsync();
    }
}
