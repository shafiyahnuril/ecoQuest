using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Contracts.Services;
using EcoQuest.Data.Errors;
using EcoQuest.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace EcoQuest.Tests.Services;

public class LeaderboardServiceTests
{
    private static readonly Guid MeId = Guid.NewGuid();

    private static (LeaderboardService Sut, ILeaderboardRepository Repo) Make()
    {
        var session = Substitute.For<ISessionService>();
        session.RequireUser().Returns(new UserSession(MeId, "alya", "alya@tes.local", "Yogyakarta", null));
        var repo = Substitute.For<ILeaderboardRepository>();
        return (new LeaderboardService(session, repo), repo);
    }

    [Fact]
    public async Task Maps_rows_to_dto_without_user_id()
    {
        var (sut, repo) = Make();
        repo.GetAsync(MeId, 20, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new LeaderboardRow(1, "rani", 41.2m, "Eco Champion", false),
            new LeaderboardRow(25, "alya", 3.4m, null, true)
        });

        var r = await sut.GetLeaderboardAsync();

        Assert.True(r.IsSuccess);
        Assert.Equal(new LeaderboardEntryDto(1, "rani", 41.2m, "Eco Champion", false), r.Value![0]);
        Assert.Equal(new LeaderboardEntryDto(25, "alya", 3.4m, null, true), r.Value[1]);
        Assert.Equal(new[] { "IsCurrentUser", "LevelName", "Rank", "TotalCo2Kg", "UserName" },
            typeof(LeaderboardEntryDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task DataAccessException_becomes_failure()
    {
        var (sut, repo) = Make();
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataAccessException(ErrorCode.NetworkError, "app_get_leaderboard"));

        var r = await sut.GetLeaderboardAsync();

        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, r.Error);
        Assert.Equal(Messages.NetworkError, r.Message);
    }
}