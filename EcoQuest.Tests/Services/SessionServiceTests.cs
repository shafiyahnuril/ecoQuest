using System;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Services;
using Xunit;

namespace EcoQuest.Tests.Services;

public class SessionServiceTests
{
    private static UserSession SampleSession(string city = "Yogyakarta") =>
        new(Guid.Parse("6f1c2b0e-0000-0000-0000-000000000001"), "dinda", "dinda@eco.id", city, null);

    [Fact]
    public void Initially_unauthenticated_and_RequireUser_throws()
    {
        var sut = new SessionService();

        Assert.Null(sut.Current);
        Assert.False(sut.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => sut.RequireUser());
    }

    [Fact]
    public void Start_Update_and_End_manage_session_and_raise_SessionChanged()
    {
        var sut = new SessionService();
        int notifications = 0;
        sut.SessionChanged += (_, _) => notifications++;

        var initial = SampleSession("Yogyakarta");
        sut.Start(initial);

        Assert.True(sut.IsAuthenticated);
        Assert.Equal(initial, sut.Current);
        Assert.Equal(initial, sut.RequireUser());
        Assert.Equal(1, notifications);

        var updated = initial with { City = "Bandung", AvatarPath = "avatars/dinda.png" };
        sut.Update(updated);

        Assert.Equal(updated, sut.Current);
        Assert.Equal(2, notifications);

        sut.End();

        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.Current);
        Assert.Throws<InvalidOperationException>(() => sut.RequireUser());
        Assert.Equal(3, notifications);
    }

    [Fact]
    public void Start_and_Update_reject_null_session()
    {
        var sut = new SessionService();

        Assert.Throws<ArgumentNullException>(() => sut.Start(null!));
        Assert.Throws<ArgumentNullException>(() => sut.Update(null!));
    }
}
