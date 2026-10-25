using EcoQuest.Core.Common;
using EcoQuest.ViewModels;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

internal sealed class TestViewModel : ViewModelBase
{
    public List<LoadState> StatesSeen { get; } = [];

    public TestViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(State))
            {
                StatesSeen.Add(State);
            }
        };
    }

    public Task<T?> Run<T>(Func<CancellationToken, Task<Result<T>>> op, CancellationToken ct = default, bool silent = false)
        => RunAsync(op, ct, silent);

    public Task<bool> Run(Func<CancellationToken, Task<Result>> op, CancellationToken ct = default, bool silent = false)
        => RunAsync(op, ct, silent);
}

/// <summary>ViewModelBase.RunAsync (SDD-Frontend 2.1): state, error banner, re-entrancy, cancellation, unexpected exceptions, silent refresh.</summary>
public class RunAsyncTests
{
    private static Result<int> Fail(string message) => Result<int>.Failure(ErrorCode.NetworkError, message);

    [Fact]
    public async Task Success_sets_Loaded_returns_value_and_resets_IsBusy()
    {
        var vm = new TestViewModel();

        var value = await vm.Run(_ => Task.FromResult(Result<int>.Success(42)));

        Assert.Equal(42, value);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
        Assert.Equal([LoadState.Loading, LoadState.Loaded], vm.StatesSeen);
    }

    [Fact]
    public async Task Failed_result_sets_Error_with_message_and_returns_default()
    {
        var vm = new TestViewModel();

        var value = await vm.Run(_ => Task.FromResult(Fail("Tidak dapat terhubung ke server.")));

        Assert.Equal(0, value);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("Tidak dapat terhubung ke server.", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Success_after_error_clears_the_message()
    {
        var vm = new TestViewModel();
        await vm.Run(_ => Task.FromResult(Fail("gagal")));

        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)));

        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Does_not_touch_IsEmpty()
    {
        var vm = new TestViewModel { IsEmpty = true };

        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)));
        Assert.True(vm.IsEmpty);

        await vm.Run(_ => Task.FromResult(Fail("x")));
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Non_generic_overload_reports_success_and_failure()
    {
        var vm = new TestViewModel();

        Assert.True(await vm.Run(_ => Task.FromResult(Result.Success())));
        Assert.Equal(LoadState.Loaded, vm.State);

        Assert.False(await vm.Run(_ => Task.FromResult(Result.Failure(ErrorCode.Unknown, "boom"))));
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("boom", vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Second_call_while_busy_returns_default_without_running()
    {
        var vm = new TestViewModel();
        var gate = new TaskCompletionSource<Result<int>>();
        var runs = 0;

        var first = vm.Run(_ => { runs++; return gate.Task; });
        Assert.True(vm.IsBusy);

        var second = await vm.Run(_ => { runs++; return Task.FromResult(Result<int>.Success(7)); });
        var secondFlag = await vm.Run(_ => { runs++; return Task.FromResult(Result.Success()); });

        Assert.Equal(0, second);
        Assert.False(secondFlag);
        Assert.Equal(1, runs);

        gate.SetResult(Result<int>.Success(5));
        Assert.Equal(5, await first);
        Assert.False(vm.IsBusy);
        Assert.Equal(LoadState.Loaded, vm.State);
    }

    [Fact]
    public async Task Cancellation_is_not_an_error_and_restores_previous_state()
    {
        var vm = new TestViewModel();
        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)));
        using var cts = new CancellationTokenSource();

        var task = vm.Run<int>(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Result<int>.Success(2);
        }, cts.Token);
        Assert.Equal(LoadState.Loading, vm.State);
        cts.Cancel();

        Assert.Equal(0, await task);
        Assert.Equal(LoadState.Loaded, vm.State);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Cancellation_on_first_load_goes_back_to_Idle()
    {
        var vm = new TestViewModel();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await vm.Run<int>(ct => Task.FromCanceled<Result<int>>(ct), cts.Token);

        Assert.Equal(0, result);
        Assert.Equal(LoadState.Idle, vm.State);
        Assert.NotEqual(LoadState.Error, vm.State);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Unexpected_exception_propagates_but_restores_state_and_IsBusy()
    {
        var vm = new TestViewModel();
        await vm.Run(_ => Task.FromResult(Fail("lama")));
        Assert.Equal(LoadState.Error, vm.State);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.Run<int>(_ => throw new InvalidOperationException("bug")));

        Assert.False(vm.IsBusy);
        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("lama", vm.ErrorMessage);
    }

    [Fact]
    public async Task Unexpected_exception_on_first_load_does_not_leave_Loading()
    {
        var vm = new TestViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.Run(_ => Task.FromException<Result>(new InvalidOperationException("bug"))));

        Assert.Equal(LoadState.Idle, vm.State);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Silent_refresh_keeps_Loaded_and_only_toggles_IsBusy()
    {
        var vm = new TestViewModel();
        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)));
        vm.StatesSeen.Clear();
        var gate = new TaskCompletionSource<Result<int>>();

        var task = vm.Run(_ => gate.Task, silent: true);
        Assert.True(vm.IsBusy);
        Assert.Equal(LoadState.Loaded, vm.State);

        gate.SetResult(Result<int>.Success(2));
        Assert.Equal(2, await task);
        Assert.False(vm.IsBusy);
        Assert.DoesNotContain(LoadState.Loading, vm.StatesSeen);
    }

    [Fact]
    public async Task Silent_refresh_that_fails_shows_the_error()
    {
        var vm = new TestViewModel();
        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)));

        await vm.Run(_ => Task.FromResult(Fail("gagal muat ulang")), silent: true);

        Assert.Equal(LoadState.Error, vm.State);
        Assert.Equal("gagal muat ulang", vm.ErrorMessage);
    }

    [Theory]
    [InlineData(LoadState.Idle)]
    [InlineData(LoadState.Error)]
    public async Task Silent_is_ignored_unless_already_Loaded(LoadState before)
    {
        var vm = new TestViewModel { State = before };
        vm.StatesSeen.Clear();

        await vm.Run(_ => Task.FromResult(Result<int>.Success(1)), silent: true);

        Assert.Contains(LoadState.Loading, vm.StatesSeen);
        Assert.Equal(LoadState.Loaded, vm.State);
    }
}
