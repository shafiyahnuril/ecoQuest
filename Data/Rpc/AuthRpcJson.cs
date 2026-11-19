using System;
using Newtonsoft.Json;

namespace EcoQuest.Data.Rpc;

public sealed class AvailabilityJson
{
    [JsonProperty("usernametaken")] public bool UserNameTaken { get; set; }
    [JsonProperty("emailtaken")]    public bool EmailTaken { get; set; }
}

public sealed class RegisteredUserJson
{
    [JsonProperty("userid")]    public Guid UserId { get; set; }
    [JsonProperty("username")]  public string UserName { get; set; } = "";
    [JsonProperty("email")]     public string Email { get; set; } = "";
    [JsonProperty("city")]      public string City { get; set; } = "";
    [JsonProperty("avatarurl")] public string? AvatarUrl { get; set; }
    [JsonProperty("createdat")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LoginRecordJson
{
    [JsonProperty("userid")]       public Guid UserId { get; set; }
    [JsonProperty("username")]     public string UserName { get; set; } = "";
    [JsonProperty("email")]        public string Email { get; set; } = "";
    [JsonProperty("passwordhash")] public string PasswordHash { get; set; } = "";
    [JsonProperty("city")]         public string City { get; set; } = "";
    [JsonProperty("avatarurl")]    public string? AvatarUrl { get; set; }
}

public sealed class ProfileJson
{
    [JsonProperty("userid")]           public Guid UserId { get; set; }
    [JsonProperty("username")]         public string UserName { get; set; } = "";
    [JsonProperty("email")]            public string Email { get; set; } = "";
    [JsonProperty("city")]             public string City { get; set; } = "";
    [JsonProperty("avatarurl")]        public string? AvatarUrl { get; set; }
    [JsonProperty("totalpoints")]      public int TotalPoints { get; set; }
    [JsonProperty("totalco2savedkg")]  public decimal TotalCo2SavedKg { get; set; }
    [JsonProperty("currentlevelid")]   public int? CurrentLevelId { get; set; }
    [JsonProperty("currentstreak")]    public int CurrentStreak { get; set; }
    [JsonProperty("createdat")]        public DateTimeOffset CreatedAt { get; set; }
    [JsonProperty("lastreviewdate")]   public string? LastReviewDate { get; set; }
    [JsonProperty("totalreviewcount")] public int TotalReviewCount { get; set; }
    [JsonProperty("totallogcount")]    public int TotalLogCount { get; set; }
}
