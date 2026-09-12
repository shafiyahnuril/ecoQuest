namespace EcoQuest.Models
{
    public enum CriteriaType
    {
        ActivityCount,
        PointsThreshold,
        StreakDays,
        CO2SavedThreshold
    }

    public class Badge
    {
        private int _badgeId;
        private string _name;
        private string _description;
        private CriteriaType _criteriaType;

        public int BadgeId { get => _badgeId; set => _badgeId = value; }
        public string Name { get => _name; set => _name = value; }
        public string Description { get => _description; set => _description = value; }
        public CriteriaType CriteriaType { get => _criteriaType; set => _criteriaType = value; }

        public Badge() { }

        public Badge(int badgeId, string name, string description, CriteriaType criteriaType)
        {
            BadgeId = badgeId;
            Name = name;
            Description = description;
            CriteriaType = criteriaType;
        }
    }
}
