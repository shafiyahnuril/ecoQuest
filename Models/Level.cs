namespace EcoQuest.Models
{
    public class Level
    {
        private int _levelId;
        private string _levelName;
        private int _minPoints;
        private int? _maxPoints;

        public int LevelId { get => _levelId; set => _levelId = value; }
        public string LevelName { get => _levelName; set => _levelName = value; }
        public int MinPoints { get => _minPoints; set => _minPoints = value; }
        public int? MaxPoints { get => _maxPoints; set => _maxPoints = value; }

        public Level() { }

        public Level(int levelId, string levelName, int minPoints, int? maxPoints)
        {
            LevelId = levelId;
            LevelName = levelName;
            MinPoints = minPoints;
            MaxPoints = maxPoints;
        }
    
        public bool IsPointInRange(int points)
        {
            return points >= MinPoints && (MaxPoints == null || points <= MaxPoints);
        }
    }
}
