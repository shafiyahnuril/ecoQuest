using System;
using System.Collections.Generic;
using EcoQuest.Data.Rpc;
using Xunit;

namespace EcoQuest.Tests.Data;

public class RpcJsonTests
{
    [Fact]
    public void AvailabilityJson_deserializes_lowercase_flags()
    {
        var dto = RpcClient.Deserialize<AvailabilityJson>("{\"usernametaken\":true,\"emailtaken\":false}");
        Assert.NotNull(dto);
        Assert.True(dto.UserNameTaken);
        Assert.False(dto.EmailTaken);
    }

    [Fact]
    public void RegisteredUserJson_and_ProfileJson_deserialize_with_nullable_fields()
    {
        var id = "6f1c2b0e-0000-0000-0000-000000000001";
        var regJson = $"{{\"userid\":\"{id}\",\"username\":\"dinda\",\"email\":\"d@x.com\",\"city\":\"Yogyakarta\",\"avatarurl\":null,\"createdat\":\"2026-11-01T08:00:00+07:00\"}}";
        var reg = RpcClient.Deserialize<RegisteredUserJson>(regJson);
        Assert.NotNull(reg);
        Assert.Equal(Guid.Parse(id), reg.UserId);
        Assert.Equal("dinda", reg.UserName);
        Assert.Null(reg.AvatarUrl);

        var profJson = $"{{\"userid\":\"{id}\",\"username\":\"dinda\",\"email\":\"d@x.com\",\"city\":\"Yogyakarta\",\"avatarurl\":\"avatars/a.png\",\"totalpoints\":125,\"totalco2savedkg\":6.2500,\"currentlevelid\":null,\"currentstreak\":0,\"createdat\":\"2026-11-01T08:00:00+07:00\",\"lastreviewdate\":null,\"totalreviewcount\":0,\"totallogcount\":0}}";
        var prof = RpcClient.Deserialize<ProfileJson>(profJson);
        Assert.NotNull(prof);
        Assert.Equal(125, prof.TotalPoints);
        Assert.Equal(6.2500m, prof.TotalCo2SavedKg);
        Assert.Null(prof.CurrentLevelId);
        Assert.Null(prof.LastReviewDate);
        Assert.Equal("avatars/a.png", prof.AvatarUrl);
    }

    [Fact]
    public void LoginRecord_array_with_one_element_deserializes()
    {
        var json = "[{\"userid\":\"6f1c2b0e-0000-0000-0000-000000000001\",\"username\":\"dinda\",\"email\":\"d@x.com\",\"passwordhash\":\"$2a$11$abc\",\"city\":\"Yogyakarta\",\"avatarurl\":null}]";
        var list = RpcClient.Deserialize<List<LoginRecordJson>>(json);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("dinda", list[0].UserName);
        Assert.Equal("$2a$11$abc", list[0].PasswordHash);
    }

    [Fact]
    public void ReviewJson_deserializes_nested_logs_and_SubmitResultJson()
    {
        var revId = "aaaaaaaa-0000-0000-0000-000000000001";
        var logId = "bbbbbbbb-0000-0000-0000-000000000002";
        var json = $"{{\"reviewid\":\"{revId}\",\"reviewdate\":\"2026-11-13\",\"totalco2savedkg\":1.1825,\"totalpointsearned\":26,\"submittedat\":\"2026-11-13T21:00:00+07:00\",\"logs\":[{{\"logid\":\"{logId}\",\"activitytypeid\":3,\"quantity\":2.5,\"starttime\":\"21:00:00\",\"endtime\":\"23:30:00\",\"note\":\"kamar\",\"photopath\":null,\"co2savedkg\":0.0325,\"pointsearned\":1,\"co2coefficientused\":0.013,\"pointfactorused\":0.8}}]}}";

        var review = RpcClient.Deserialize<ReviewJson>(json);
        Assert.NotNull(review);
        Assert.Equal(Guid.Parse(revId), review.ReviewId);
        Assert.Equal("2026-11-13", review.ReviewDate);
        Assert.Equal(1.1825m, review.TotalCo2SavedKg);
        Assert.Equal(26, review.TotalPointsEarned);
        Assert.Single(review.Logs);
        Assert.Equal("21:00:00", review.Logs[0].StartTime);
        Assert.Equal(0.013m, review.Logs[0].Co2CoefficientUsed);
        Assert.Equal(0.8m, review.Logs[0].PointFactorUsed);

        var submit = RpcClient.Deserialize<SubmitResultJson>($"{{\"reviewid\":\"{revId}\",\"submittedat\":\"2026-11-13T21:00:00+07:00\"}}");
        Assert.NotNull(submit);
        Assert.Equal(Guid.Parse(revId), submit.ReviewId);
    }

    [Fact]
    public void Analytics_and_HistoryJson_all_18_columns_deserialize()
    {
        var daily = RpcClient.Deserialize<List<DailyTotalJson>>("[{\"reviewdate\":\"2026-11-13\",\"totalco2savedkg\":0.45,\"totalpointsearned\":9}]");
        Assert.Single(daily!);
        Assert.Equal(0.45m, daily![0].TotalCo2SavedKg);

        var cats = RpcClient.Deserialize<List<CategoryTotalJson>>("[{\"categoryid\":1,\"categoryname\":\"Transportasi\",\"totalco2savedkg\":0.90}]");
        Assert.Single(cats!);
        Assert.Equal("Transportasi", cats![0].CategoryName);

        var histJson = "[{\"logid\":\"bbbbbbbb-0000-0000-0000-000000000001\",\"reviewid\":\"aaaaaaaa-0000-0000-0000-000000000001\",\"logdate\":\"2026-11-13\",\"activitytypeid\":1,\"typename\":\"Jalan Kaki\",\"categoryid\":1,\"categoryname\":\"Transportasi\",\"inputcontroltype\":\"Numeric\",\"unitlabel\":\"km\",\"quantity\":3.5,\"starttime\":null,\"endtime\":null,\"co2savedkg\":0.5250,\"pointsearned\":11,\"co2coefficientused\":0.15,\"pointfactorused\":1.0,\"note\":null,\"photopath\":\"logs/1.jpg\"}]";
        var history = RpcClient.Deserialize<List<HistoryJson>>(histJson);
        Assert.Single(history!);
        var row = history![0];
        Assert.Equal("2026-11-13", row.LogDate);
        Assert.Equal("Numeric", row.InputControlType);
        Assert.Equal(3.5m, row.Quantity);
        Assert.Equal(0.5250m, row.Co2SavedKg);
        Assert.Equal(11, row.PointsEarned);
        Assert.Equal("logs/1.jpg", row.PhotoPath);
    }

    [Fact]
    public void Gamification_UserBadgeJson_and_LeaderboardJson_deserialize()
    {
        var badges = RpcClient.Deserialize<List<UserBadgeJson>>("[{\"badgeid\":1,\"earnedat\":\"2026-11-13T21:05:00+07:00\"}]");
        Assert.Single(badges!);
        Assert.Equal(1, badges![0].BadgeId);

        var board = RpcClient.Deserialize<List<LeaderboardJson>>("[{\"rank\":10000000001,\"username\":\"dinda\",\"totalco2savedkg\":12.5,\"levelname\":null,\"iscurrentuser\":true}]");
        Assert.Single(board!);
        Assert.Equal(10000000001L, board![0].Rank);
        Assert.Null(board![0].LevelName);
        Assert.True(board![0].IsCurrentUser);
    }
}
