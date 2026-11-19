using System;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface IUserRepository
{
    Task<(bool UserNameTaken, bool EmailTaken)> CheckAvailabilityAsync(string userName, string email, CancellationToken ct);
    Task<User> RegisterAsync(string userName, string email, string passwordHash, string city, CancellationToken ct); // app_register
    Task<LoginRecord?> GetLoginRecordAsync(string identifier, CancellationToken ct);                                  // app_get_login_record
    Task<UserProfileSnapshot?> GetProfileAsync(Guid userId, CancellationToken ct);                                     // app_get_profile
    Task UpdateProfileAsync(Guid userId, string city, string? avatarPath, CancellationToken ct);                       // app_update_profile
    Task UpdatePasswordHashAsync(Guid userId, string newHash, CancellationToken ct);                                  // app_update_password
}

public sealed record LoginRecord(Guid UserId, string UserName, string Email, string PasswordHash, string City, string? AvatarPath);

public sealed record UserProfileSnapshot(User User, DateOnly? LastReviewDate, int TotalReviewCount, int TotalLogCount);
