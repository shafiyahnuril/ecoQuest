using Postgrest.Attributes;
using Postgrest.Models;

namespace EcoQuest.Data.Records;

[Table("badges")]
public sealed class BadgeRecord : BaseModel
{
    [PrimaryKey("badgeid", false)] public int BadgeId { get; set; }
    [Column("name")]               public string Name { get; set; } = "";
    [Column("description")]        public string? Description { get; set; }
    [Column("criteriatype")]       public string CriteriaType { get; set; } = "";
    [Column("criteriavalue")]      public decimal CriteriaValue { get; set; }
}
