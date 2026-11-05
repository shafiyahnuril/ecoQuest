using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

[Table("activitytypes")]
public sealed class ActivityTypeRecord : BaseModel
{
    [PrimaryKey("activitytypeid", false)] public int ActivityTypeId { get; set; }
    [Column("typename")]                  public string TypeName { get; set; } = "";
    [Column("categoryid")]                public int CategoryId { get; set; }
    [Column("inputcontroltype")]          public string InputControlType { get; set; } = "";
    [Column("unitlabel")]                 public string UnitLabel { get; set; } = "";
    [Column("co2coefficient")]            public decimal Co2Coefficient { get; set; }
    [Column("pointfactor")]               public decimal PointFactor { get; set; }
    [Column("plausiblemaxperday")]        public decimal? PlausibleMaxPerDay { get; set; }
    [Column("hardmaxperday")]             public decimal HardMaxPerDay { get; set; }
    [Column("description")]               public string? Description { get; set; }
    [Column("displayorder")]              public int DisplayOrder { get; set; }
}
