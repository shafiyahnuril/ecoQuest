using System;
using Newtonsoft.Json;

namespace EcoQuest.Data.Rpc;

public sealed class UserBadgeJson
{
    [JsonProperty("badgeid")]  public int BadgeId { get; set; }
    [JsonProperty("earnedat")] public DateTimeOffset EarnedAt { get; set; }
}

public sealed class LeaderboardJson
{
    [JsonProperty("rank")]            public long Rank { get; set; }
    [JsonProperty("username")]        public string UserName { get; set; } = "";
    [JsonProperty("totalco2savedkg")] public decimal TotalCo2SavedKg { get; set; }
    [JsonProperty("levelname")]       public string? LevelName { get; set; }
    [JsonProperty("iscurrentuser")]   public bool IsCurrentUser { get; set; }
}
