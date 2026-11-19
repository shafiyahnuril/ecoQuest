using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Data.Mapping;
using EcoQuest.Data.Rpc;

namespace EcoQuest.Data.Repositories;

public sealed class SupabaseUserRepository(RpcClient rpc) : IUserRepository
{
    public async Task<(bool UserNameTaken, bool EmailTaken)> CheckAvailabilityAsync(
        string userName,
        string email,
        CancellationToken ct)
    {
        var r = await rpc.CallAsync<AvailabilityJson>(
            RpcNames.CheckAvailability,
            new() { ["p_username"] = userName, ["p_email"] = email },
            ct) ?? throw new DataAccessException(ErrorCode.Unknown, RpcNames.CheckAvailability);

        return (r.UserNameTaken, r.EmailTaken);
    }

    public async Task<User> RegisterAsync(
        string userName,
        string email,
        string passwordHash,
        string city,
        CancellationToken ct)
    {
        var j = await rpc.CallAsync<RegisteredUserJson>(
            RpcNames.Register,
            new()
            {
                ["p_username"] = userName,
                ["p_email"] = email,
                ["p_passwordhash"] = passwordHash,
                ["p_city"] = city
            },
            ct) ?? throw new DataAccessException(ErrorCode.Unknown, RpcNames.Register);

        return UserMapper.FromRegistered(j);
    }

    public async Task<LoginRecord?> GetLoginRecordAsync(string identifier, CancellationToken ct)
    {
        var rows = await rpc.CallAsync<List<LoginRecordJson>>(
            RpcNames.GetLoginRecord,
            new() { ["p_identifier"] = identifier },
            ct);

        var row = rows?.FirstOrDefault();
        return row is null ? null : UserMapper.FromLogin(row);
    }

    public async Task<UserProfileSnapshot?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var j = await rpc.CallAsync<ProfileJson>(
            RpcNames.GetProfile,
            new() { ["p_user_id"] = userId },
            ct);

        return j is null ? null : UserMapper.FromProfile(j);
    }

    public Task UpdateProfileAsync(Guid userId, string city, string? avatarPath, CancellationToken ct)
        => rpc.CallAsync(
            RpcNames.UpdateProfile,
            new()
            {
                ["p_user_id"] = userId,
                ["p_city"] = city,
                ["p_avatarurl"] = avatarPath
            },
            ct);

    public Task UpdatePasswordHashAsync(Guid userId, string newHash, CancellationToken ct)
        => rpc.CallAsync(
            RpcNames.UpdatePassword,
            new()
            {
                ["p_user_id"] = userId,
                ["p_new_passwordhash"] = newHash
            },
            ct);
}
