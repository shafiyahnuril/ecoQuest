using System;

namespace EcoQuest.Models
{
    public class User
    {
        private Guid _userId;
        private string _userName;
        private string _email;
        private string _passwordHash;
        private string _city;
        private int _totalPoints;
        private decimal _totalCO2SavedKg;
        private int? _currentLevelId;
        private int _currentStreak;
        private DateTime _createdAt;

        public Guid UserId { get => _userId; set => _userId = value; }
        public string UserName { get => _userName; set => _userName = value; }
        public string Email { get => _email; set => _email = value; }
        public string PasswordHash { get => _passwordHash; set => _passwordHash = value; }
        public string City { get => _city; set => _city = value; }
        public int TotalPoints { get => _totalPoints; set => _totalPoints = value; }
        public decimal TotalCO2SavedKg { get => _totalCO2SavedKg; set => _totalCO2SavedKg = value; }
        public int? CurrentLevelId { get => _currentLevelId; set => _currentLevelId = value; }
        public int CurrentStreak { get => _currentStreak; set => _currentStreak = value; }
        public DateTime CreatedAt { get => _createdAt; set => _createdAt = value; }

        public User() { }

        public User(Guid userId, string userName, string email, string passwordHash, string city)
        {
            UserId = userId;
            UserName = userName;
            Email = email;
            PasswordHash = passwordHash;
            City = city;
            TotalPoints = 0;
            TotalCO2SavedKg = 0;
            CurrentStreak = 0;
            CreatedAt = DateTime.Now;
        }
    }
}