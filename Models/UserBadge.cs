using System;

namespace EcoQuest.Models
{
    public class UserBadge
    {
        private int _badgeId;
        private Guid _userId;
        private DateTime _earnedAt;

        public int BadgeId { get => _badgeId; set => _badgeId = value; }
        public Guid UserId { get => _userId; set => _userId = value; }
        public DateTime EarnedAt { get => _earnedAt; set => _earnedAt = value; }

        public UserBadge() { }

        public UserBadge(int badgeId, Guid userId, DateTime earnedAt)
        {
            BadgeId = badgeId;
            UserId = userId;
            EarnedAt = earnedAt;
        }
    }
}
