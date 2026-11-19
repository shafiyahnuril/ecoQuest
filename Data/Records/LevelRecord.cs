using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

[Table("levels")]
public sealed class LevelRecord : BaseModel
{
    [PrimaryKey("levelid", false)] public int LevelId { get; set; }
    [Column("levelname")]          public string LevelName { get; set; } = "";
    [Column("minpoints")]          public int MinPoints { get; set; }
    [Column("maxpoints")]          public int? MaxPoints { get; set; }
}
