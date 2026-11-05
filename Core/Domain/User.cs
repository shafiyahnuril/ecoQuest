using System;

namespace EcoQuest.Core.Domain;

public sealed class User
{
    public Guid UserId { get; }
    public string UserName { get; }
    public string Email { get; }
    public string City { get; private set; }
    public string? AvatarPath { get; private set; }
    public int TotalPoints { get; private set; }
    public decimal TotalCo2SavedKg { get; private set; }
    public int? CurrentLevelId { get; private set; }
    public int CurrentStreak { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public User(Guid userId, string userName, string email, string city, DateTimeOffset createdAt,
                string? avatarPath = null, int totalPoints = 0, decimal totalCo2SavedKg = 0m,
                int? currentLevelId = null, int currentStreak = 0)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Username wajib diisi.", nameof(userName));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email wajib diisi.", nameof(email));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("Kota wajib diisi.", nameof(city));
        if (totalPoints < 0) throw new ArgumentOutOfRangeException(nameof(totalPoints));
        if (totalCo2SavedKg < 0) throw new ArgumentOutOfRangeException(nameof(totalCo2SavedKg));
        if (currentStreak < 0) throw new ArgumentOutOfRangeException(nameof(currentStreak));

        UserId = userId;
        UserName = userName;
        Email = email;
        City = city;
        CreatedAt = createdAt;
        AvatarPath = avatarPath;
        TotalPoints = totalPoints;
        TotalCo2SavedKg = totalCo2SavedKg;
        CurrentLevelId = currentLevelId;
        CurrentStreak = currentStreak;
    }

    public void ChangeCity(string city)
    {
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("Kota wajib diisi.", nameof(city));
        City = city;
    }

    public void SetAvatar(string? path) => AvatarPath = path;

    public void ApplyReview(decimal co2, int points, int newStreak, int? levelId)
    {
        if (co2 < 0) throw new ArgumentOutOfRangeException(nameof(co2));
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        if (newStreak < 1) throw new ArgumentOutOfRangeException(nameof(newStreak));

        TotalCo2SavedKg += co2;
        TotalPoints += points;
        CurrentStreak = newStreak;
        CurrentLevelId = levelId;
    }
}
