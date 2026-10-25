using EcoQuest.Navigation;
using EcoQuest.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
}
