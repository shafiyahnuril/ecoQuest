using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Repositories;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.Data;

public class CachedActivityTypeRepositoryTests
{
    [Fact]
    public async Task GetAllAsync_calls_inner_repository_only_once_on_consecutive_calls()
    {
        var inner = Substitute.For<IActivityTypeRepository>();
        var sampleList = new List<ActivityType>
        {
            new(1, "Sepeda", 1, InputControlType.Numeric, "km", 0.15m, 1.0m, 30m, 300m, "Desc", 1)
        };
        inner.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ActivityType>>(sampleList));

        var cached = new CachedActivityTypeRepository(inner);

        var first = await cached.GetAllAsync();
        var second = await cached.GetAllAsync();

        Assert.Same(sampleList, first);
        Assert.Same(sampleList, second);
        await inner.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
