// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Dtos;

public sealed record RegisterRequest(string UserName, string Email, string Password, string ConfirmPassword, string City);
public sealed record LoginRequest(string Identifier, string Password);           // Identifier = username ATAU email

public sealed record UserSession(Guid UserId, string UserName, string Email, string City, string? AvatarPath);

public sealed record UserProfileDto(
    Guid UserId, string UserName, string Email, string City, string? AvatarPath,
    int TotalPoints, decimal TotalCo2SavedKg, int CurrentStreak,       // CurrentStreak = streak EFEKTIF (0 bila putus)
    LevelProgressDto Level, DateOnly? LastReviewDate, int TotalReviewCount, int TotalLogCount,
    DateTimeOffset CreatedAt);

public sealed record UpdateProfileRequest(string City, string? NewAvatarSourcePath, bool RemoveAvatar);
public sealed record ChangePasswordRequest(string OldPassword, string NewPassword, string ConfirmNewPassword);
