using EcoQuest.Controls;
using Xunit;

namespace EcoQuest.Tests.ViewModels;

public class PasswordRevealStateTests
{
    [Fact]
    public void Starts_hidden()
    {
        Assert.False(new PasswordRevealState().IsRevealed);
    }

    [Fact]
    public void Toggle_switches_between_hidden_and_revealed()
    {
        var state = new PasswordRevealState();

        state.Toggle();
        Assert.True(state.IsRevealed);

        state.Toggle();
        Assert.False(state.IsRevealed);
    }

    [Fact]
    public void Busy_hides_the_password_but_leaving_busy_does_not_reveal_it()
    {
        var state = new PasswordRevealState();
        state.Set(true);

        state.OnBusyChanged(true);
        Assert.False(state.IsRevealed);

        state.OnBusyChanged(false);
        Assert.False(state.IsRevealed);
    }

    [Fact]
    public void Clearing_from_outside_hides_the_password()
    {
        var state = new PasswordRevealState();
        state.Set(true);

        state.OnPasswordChanged("", changedByUser: false);

        Assert.False(state.IsRevealed);
    }

    [Fact]
    public void Null_from_outside_also_hides_the_password()
    {
        var state = new PasswordRevealState();
        state.Set(true);

        state.OnPasswordChanged(null, changedByUser: false);

        Assert.False(state.IsRevealed);
    }

    [Fact]
    public void User_deleting_every_character_keeps_the_mode()
    {
        var state = new PasswordRevealState();
        state.Set(true);

        state.OnPasswordChanged("", changedByUser: true);

        Assert.True(state.IsRevealed);
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("Rahasia123", false)]
    [InlineData("Rahasia123", true)]
    public void A_non_empty_value_never_changes_the_mode(string value, bool byUser)
    {
        var state = new PasswordRevealState();
        state.Set(true);

        state.OnPasswordChanged(value, byUser);

        Assert.True(state.IsRevealed);
    }

    [Fact]
    public void Conceal_is_idempotent()
    {
        var state = new PasswordRevealState();
        state.Conceal();
        state.Conceal();
        Assert.False(state.IsRevealed);
    }
}
