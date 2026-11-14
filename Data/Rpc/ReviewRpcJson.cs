using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EcoQuest.Data.Rpc;

public sealed class ReviewJson
{
    [JsonProperty("reviewid")]          public Guid ReviewId { get; set; }
    [JsonProperty("reviewdate")]        public string ReviewDate { get; set; } = "";
    [JsonProperty("totalco2savedkg")]   public decimal TotalCo2SavedKg { get; set; }
    [JsonProperty("totalpointsearned")] public int TotalPointsEarned { get; set; }
    [JsonProperty("submittedat")]       public DateTimeOffset SubmittedAt { get; set; }
    [JsonProperty("logs")]              public List<ReviewLogJson> Logs { get; set; } = [];
}

public sealed class ReviewLogJson
{
    [JsonProperty("logid")]              public Guid LogId { get; set; }
    [JsonProperty("activitytypeid")]     public int ActivityTypeId { get; set; }
    [JsonProperty("quantity")]           public decimal Quantity { get; set; }
    [JsonProperty("starttime")]          public string? StartTime { get; set; }
    [JsonProperty("endtime")]            public string? EndTime { get; set; }
    [JsonProperty("note")]               public string? Note { get; set; }
    [JsonProperty("photopath")]          public string? PhotoPath { get; set; }
    [JsonProperty("co2savedkg")]         public decimal Co2SavedKg { get; set; }
    [JsonProperty("pointsearned")]       public int PointsEarned { get; set; }
    [JsonProperty("co2coefficientused")] public decimal Co2CoefficientUsed { get; set; }
    [JsonProperty("pointfactorused")]    public decimal PointFactorUsed { get; set; }
}

public sealed class SubmitResultJson
{
    [JsonProperty("reviewid")]    public Guid ReviewId { get; set; }
    [JsonProperty("submittedat")] public DateTimeOffset SubmittedAt { get; set; }
}

public sealed class DailyTotalJson
{
    [JsonProperty("reviewdate")]        public string ReviewDate { get; set; } = "";
    [JsonProperty("totalco2savedkg")]   public decimal TotalCo2SavedKg { get; set; }
    [JsonProperty("totalpointsearned")] public int TotalPointsEarned { get; set; }
}

public sealed class CategoryTotalJson
{
    [JsonProperty("categoryid")]      public int CategoryId { get; set; }
    [JsonProperty("categoryname")]    public string CategoryName { get; set; } = "";
    [JsonProperty("totalco2savedkg")] public decimal TotalCo2SavedKg { get; set; }
}

public sealed class HistoryJson
{
    [JsonProperty("logid")]              public Guid LogId { get; set; }
    [JsonProperty("reviewid")]           public Guid ReviewId { get; set; }
    [JsonProperty("logdate")]            public string LogDate { get; set; } = "";
    [JsonProperty("activitytypeid")]     public int ActivityTypeId { get; set; }
    [JsonProperty("typename")]           public string TypeName { get; set; } = "";
    [JsonProperty("categoryid")]         public int CategoryId { get; set; }
    [JsonProperty("categoryname")]       public string CategoryName { get; set; } = "";
    [JsonProperty("inputcontroltype")]   public string InputControlType { get; set; } = "";
    [JsonProperty("unitlabel")]          public string UnitLabel { get; set; } = "";
    [JsonProperty("quantity")]           public decimal Quantity { get; set; }
    [JsonProperty("starttime")]          public string? StartTime { get; set; }
    [JsonProperty("endtime")]            public string? EndTime { get; set; }
    [JsonProperty("co2savedkg")]         public decimal Co2SavedKg { get; set; }
    [JsonProperty("pointsearned")]       public int PointsEarned { get; set; }
    [JsonProperty("co2coefficientused")] public decimal Co2CoefficientUsed { get; set; }
    [JsonProperty("pointfactorused")]    public decimal PointFactorUsed { get; set; }
    [JsonProperty("note")]               public string? Note { get; set; }
    [JsonProperty("photopath")]          public string? PhotoPath { get; set; }
}
