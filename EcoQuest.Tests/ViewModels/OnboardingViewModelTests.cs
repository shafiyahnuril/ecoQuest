using System.IO;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Navigation;
using EcoQuest.ViewModels;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class OnboardingViewModelTests : IDisposable
{
    private static readonly UserSession Dinda = new(Guid.NewGuid(), "dinda", "dinda@mail.ugm.ac.id", "Yogyakarta", null);

    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IOnboardingStore _store = Substitute.For<IOnboardingStore>();
    private readonly IUiText _text = Substitute.For<IUiText>();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ecoquest-onboarding-vm-" + Guid.NewGuid().ToString("N"));

    public OnboardingViewModelTests()
    {
        _text.Get(Arg.Any<string>()).Returns(call => call.Arg<string>());   // keys stand in for the Indonesian texts
        _session.Current.Returns(Dinda);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private OnboardingViewModel Create() => new(_nav, _session, _store, _text);

    [Fact]
    public void Starts_on_step_one_with_its_texts_and_indicator()
    {
        var vm = Create();

        Assert.Equal(0, vm.StepIndex);
        Assert.Equal("Onboarding.1.Title", vm.StepTitle);
        Assert.Equal("Onboarding.1.Body", vm.StepBody);
        Assert.Equal("1/3", vm.StepIndicator);
        Assert.Equal("Onboarding.Next", vm.NextLabel);
        Assert.False(vm.IsLastStep);
        Assert.Equal([true, false, false], vm.Dots.Select(d => d.IsActive));
    }

    [Fact]
    public async Task Next_moves_1_2_3_and_the_last_step_is_labelled_Start()
    {
        var vm = Create();

        await vm.NextCommand.ExecuteAsync(null);
        Assert.Equal("2/3", vm.StepIndicator);
        Assert.Equal("Onboarding.2.Title", vm.StepTitle);
        Assert.Equal("Onboarding.Next", vm.NextLabel);
        Assert.Equal([false, true, false], vm.Dots.Select(d => d.IsActive));

        await vm.NextCommand.ExecuteAsync(null);
        Assert.Equal("3/3", vm.StepIndicator);
        Assert.Equal("Onboarding.3.Body", vm.StepBody);
        Assert.Equal("Onboarding.Start", vm.NextLabel);
        Assert.True(vm.IsLastStep);
        Assert.Equal([false, false, true], vm.Dots.Select(d => d.IsActive));

        _store.DidNotReceive().MarkCompleted(Arg.Any<Guid>());
        await _nav.DidNotReceive().NavigateTo<DashboardViewModel>(Arg.Any<object?>());
    }

    [Fact]
    public async Task Skip_marks_the_user_done_clears_history_and_opens_the_dashboard()
    {
        var vm = Create();

        await vm.SkipCommand.ExecuteAsync(null);

        _store.Received(1).MarkCompleted(Dinda.UserId);
        Received.InOrder(() =>
        {
            _nav.ClearHistory();
            _nav.NavigateTo<DashboardViewModel>(null);
        });
    }

    [Fact]
    public async Task Start_on_the_last_step_does_the_same_as_Skip()
    {
        var vm = Create();
        await vm.NextCommand.ExecuteAsync(null);
        await vm.NextCommand.ExecuteAsync(null);

        await vm.NextCommand.ExecuteAsync(null);

        _store.Received(1).MarkCompleted(Dinda.UserId);
        _nav.Received(1).ClearHistory();
        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
    }

    [Fact]
    public async Task A_store_that_cannot_write_does_not_block_navigation()
    {
        // A file where the data folder should be: every write fails, the store must swallow it.
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var vm = new OnboardingViewModel(_nav, _session, new FileOnboardingStore(Path.Combine(blocker, "onboarding.json")), _text);

        await vm.SkipCommand.ExecuteAsync(null);

        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
    }

    [Fact]
    public async Task Without_a_session_nothing_is_stored_but_the_dashboard_still_opens()
    {
        _session.Current.Returns((UserSession?)null);

        await Create().SkipCommand.ExecuteAsync(null);

        _store.DidNotReceive().MarkCompleted(Arg.Any<Guid>());
        await _nav.Received(1).NavigateTo<DashboardViewModel>(null);
    }
}
