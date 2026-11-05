using System;
using System.Net.Http;
using System.Threading.Tasks;
using EcoQuest.Core.Common;

namespace EcoQuest.Data.Errors;

public static class SupabaseErrorMapper
{
    private static readonly (string Token, ErrorCode Code)[] Tokens =
    [
        ("REVIEW_ALREADY_SUBMITTED", ErrorCode.ReviewAlreadySubmitted),
        ("REVIEW_DATE_MISMATCH",     ErrorCode.ReviewDateMismatch),
        ("USERNAME_TAKEN",           ErrorCode.UsernameTaken),
        ("EMAIL_TAKEN",              ErrorCode.EmailTaken),
        ("USER_NOT_FOUND",           ErrorCode.NotFound),
        ("VALIDATION_FAILED",        ErrorCode.ValidationFailed),
    ];

    public static ErrorCode Map(Exception ex) => ex switch
    {
        Postgrest.Exceptions.PostgrestException pe => FromMessage(pe.Message),
        HttpRequestException or TaskCanceledException or TimeoutException => ErrorCode.NetworkError,
        System.Net.Sockets.SocketException => ErrorCode.NetworkError,
        _ => ErrorCode.Unknown
    };

    public static ErrorCode FromMessage(string? message)
    {
        if (string.IsNullOrEmpty(message)) return ErrorCode.Unknown;
        foreach (var (token, code) in Tokens)
            if (message.Contains(token, StringComparison.Ordinal)) return code;
        return ErrorCode.Unknown;
    }
}
