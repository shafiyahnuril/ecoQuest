using System;
using System.Net.Http;
using EcoQuest.Core.Common;
using EcoQuest.Data.Errors;
using Xunit;

namespace EcoQuest.Tests.Data;

public class SupabaseErrorMapperTests
{
    [Theory]
    [InlineData("{\"code\":\"P0001\",\"message\":\"REVIEW_ALREADY_SUBMITTED\"}", ErrorCode.ReviewAlreadySubmitted)]
    [InlineData("{\"message\":\"REVIEW_DATE_MISMATCH\"}", ErrorCode.ReviewDateMismatch)]
    [InlineData("{\"message\":\"USERNAME_TAKEN\"}", ErrorCode.UsernameTaken)]
    [InlineData("{\"message\":\"EMAIL_TAKEN\"}", ErrorCode.EmailTaken)]
    [InlineData("{\"message\":\"USER_NOT_FOUND\"}", ErrorCode.NotFound)]
    [InlineData("{\"message\":\"VALIDATION_FAILED\"}", ErrorCode.ValidationFailed)]
    [InlineData("{\"message\":\"something else\"}", ErrorCode.Unknown)]
    public void Maps_postgres_error_tokens(string message, ErrorCode expected)
        => Assert.Equal(expected, SupabaseErrorMapper.FromMessage(message));

    [Fact]
    public void HttpRequestException_is_NetworkError()
        => Assert.Equal(ErrorCode.NetworkError, SupabaseErrorMapper.Map(new HttpRequestException("down")));

    [Fact]
    public void Unknown_exception_is_Unknown()
        => Assert.Equal(ErrorCode.Unknown, SupabaseErrorMapper.Map(new InvalidOperationException()));
}
