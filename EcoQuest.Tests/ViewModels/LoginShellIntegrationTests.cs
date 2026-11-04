using System.Runtime.CompilerServices;
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

/// <summary>Login through the fake auth service must open the session that feeds the shell's account block (UIR-24/25).</summary>
public class LoginShellIntegrationTests
{
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IEveningReviewService _review = Substitute.For<IEveningReviewService>();

    private static T Screen<T>() where T : ViewModelBase => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private ShellViewModel CreateShell(FakeWorld world) =>
        new(_nav, Substitute.For<IDialogService>(), Substitute.For<IUiText>(), new ToastViewModel(), world.Session, world.Auth, _review);

    [Fact]
    public async Task Successful_login_makes_the_shell_authenticated_with_the_user_name()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var shell = CreateShell(world);
        _review.GetTodayAsync(Arg.Any<CancellationToken>()).Returns(
            Result<TodayReviewDto>.Success(new TodayReviewDto(TodayReviewState.NotSubmitted, new DateOnly(2026, 10, 25), null)));
        var login = new LoginViewModel(world.Auth, _nav) { Identifier = FakeData.DemoUserName, Password = FakeData.DemoPassword };

        // After a successful login the navigation service shows the dashboard; reproduce that for the shell.
        _nav.When(n => n.NavigateTo<DashboardViewModel>(Arg.Any<object?>())).Do(_ =>
        {
            _nav.Current.Returns(Screen<DashboardViewModel>());
            _nav.Navigated += Raise.Event();
        });

        await login.LoginCommand.ExecuteAsync(null);

        Assert.True(shell.IsAuthenticated);
        Assert.Equal("dinda", shell.DisplayName);
        Assert.Equal("D", shell.AvatarInitial);
        Assert.NotNull(shell.MainViewModel);
    }

    [Fact]
    public async Task Failed_login_leaves_the_shell_unauthenticated()
    {
        var world = new FakeWorld(FakeScenario.Normal);
        var shell = CreateShell(world);
        var login = new LoginViewModel(world.Auth, _nav) { Identifier = FakeData.DemoUserName, Password = "salah123" };

        await login.LoginCommand.ExecuteAsync(null);

        Assert.False(shell.IsAuthenticated);
        Assert.Equal("", shell.DisplayName);
        Assert.NotNull(login.ErrorMessage);
    }
}
