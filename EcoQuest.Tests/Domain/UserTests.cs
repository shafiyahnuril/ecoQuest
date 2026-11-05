using System;
using EcoQuest.Core.Domain;
using Xunit;

namespace EcoQuest.Tests.Domain;

public class UserTests
{
    [Fact]
    public void ApplyReview_negative_throws()
    {
        var user = new User(Guid.NewGuid(), "budi", "budi@mail.com", "Yogyakarta", DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => user.ApplyReview(-1m, 5, 1, null));
    }
}
