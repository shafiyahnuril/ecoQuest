using EcoQuest.Infrastructure;
using EcoQuest.Navigation;
using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class NavigationServiceTests
{
    public sealed class ScreenA : ViewModelBase
    {
        public object? Parameter { get; private set; }
        public int NavigatedFromCount { get; private set; }
        public override Task OnNavigatedToAsync(object? parameter) { Parameter = parameter; return Task.CompletedTask; }
        public override void OnNavigatedFrom() => NavigatedFromCount++;
    }

    public sealed class ScreenB : ViewModelBase { }

    private static NavigationService Create()
    {
        var services = new ServiceCollection();
        services.AddTransient<ScreenA>();
        services.AddTransient<ScreenB>();
        services.AddTransient<ConnectionErrorViewModel>();
        services.AddSingleton(new AppConfig(null, null, null, UseFakes: true));
        services.AddSingleton(Substitute.For<INavigationService>());
        services.AddSingleton(Substitute.For<IUiText>());
        return new NavigationService(services.BuildServiceProvider());
    }

    [Fact]
    public async Task NavigateTo_sets_current_raises_event_and_passes_parameter()
    {
        var nav = Create();
        var raised = 0;
        nav.Navigated += (_, _) => raised++;

        await nav.NavigateTo<ScreenA>("p");

        var a = Assert.IsType<ScreenA>(nav.Current);
        Assert.Equal("p", a.Parameter);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task NavigateTo_leaves_the_previous_screen_and_builds_a_fresh_instance()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        var first = (ScreenA)nav.Current!;

        await nav.NavigateTo<ScreenA>();

        Assert.Equal(1, first.NavigatedFromCount);
        Assert.NotSame(first, nav.Current);
    }

    [Fact]
    public async Task ClearHistory_forgets_current_screen_without_raising_navigated()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        var a = (ScreenA)nav.Current!;
        var raised = 0;
        nav.Navigated += (_, _) => raised++;

        nav.ClearHistory();

        Assert.Null(nav.Current);
        Assert.Equal(1, a.NavigatedFromCount);
        Assert.Equal(0, raised);

        await nav.NavigateTo<ScreenB>();
        Assert.IsType<ScreenB>(nav.Current);
    }

    [Fact]
    public async Task First_navigation_has_nothing_to_go_back_to()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();

        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public async Task Leaving_a_screen_records_it_and_GoBack_returns_a_fresh_instance_with_its_parameter()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>("p");
        var original = (ScreenA)nav.Current!;
        await nav.NavigateTo<ConnectionErrorViewModel>();

        Assert.True(nav.CanGoBack);
        await nav.GoBackAsync();

        var restored = Assert.IsType<ScreenA>(nav.Current);
        Assert.NotSame(original, restored);
        Assert.Equal("p", restored.Parameter);
        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public async Task GoBack_without_history_does_nothing()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        var current = nav.Current;
        var raised = 0;
        nav.Navigated += (_, _) => raised++;

        await nav.GoBackAsync();

        Assert.Same(current, nav.Current);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Connection_error_never_becomes_the_screen_to_go_back_to()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        await nav.NavigateTo<ConnectionErrorViewModel>();
        await nav.NavigateTo<ConnectionErrorViewModel>();

        await nav.GoBackAsync();

        Assert.IsType<ScreenA>(nav.Current);
    }

    [Fact]
    public async Task Navigating_to_the_same_screen_type_keeps_it_as_the_screen_to_go_back_to()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        await nav.NavigateTo<ScreenA>();
        await nav.NavigateTo<ConnectionErrorViewModel>();

        await nav.GoBackAsync();

        Assert.IsType<ScreenA>(nav.Current);
    }

    [Fact]
    public async Task ClearHistory_forgets_the_screen_to_go_back_to()
    {
        var nav = Create();
        await nav.NavigateTo<ScreenA>();
        await nav.NavigateTo<ScreenB>();
        Assert.True(nav.CanGoBack);

        nav.ClearHistory();

        Assert.False(nav.CanGoBack);
        await nav.NavigateTo<ScreenA>();
        Assert.False(nav.CanGoBack);
    }
}
