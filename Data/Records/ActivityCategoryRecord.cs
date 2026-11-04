using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

[Table("activitycategories")]
public sealed class ActivityCategoryRecord : BaseModel
{
    [PrimaryKey("categoryid", false)] public int CategoryId { get; set; }
    [Column("categoryname")]          public string CategoryName { get; set; } = "";
    [Column("description")]           public string? Description { get; set; }
}
