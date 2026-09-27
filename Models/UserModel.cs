using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ecoQuest.Models
{
    // Nama tabel disesuaikan dengan yang ada di ecoquest_schema.sql (huruf kecil semua karena postgres bersifat case-insensitive, atau string persis "Users")
    [Table("users")]
    public class UserModel : BaseModel
    {
        [PrimaryKey("userid", false)]
        public Guid UserId { get; set; }

        [Column("username")]
        public string UserName { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("passwordhash")]
        public string PasswordHash { get; set; }

        [Column("city")]
        public string City { get; set; }

        [Column("avatarurl")]
        public string AvatarUrl { get; set; }

        [Column("totalpoints")]
        public int TotalPoints { get; set; }

        [Column("totalco2savedkg")]
        public decimal TotalCO2SavedKg { get; set; }

        [Column("currentlevelid")]
        public int? CurrentLevelId { get; set; }

        [Column("currentstreak")]
        public int CurrentStreak { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }
    }
}
