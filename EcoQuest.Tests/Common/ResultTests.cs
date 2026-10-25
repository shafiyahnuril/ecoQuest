using EcoQuest.Core.Common;
using Xunit;

namespace EcoQuest.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var r = Result.Success();
        Assert.True(r.IsSuccess);
        Assert.Equal(ErrorCode.None, r.Error);
        Assert.Null(r.Message);
        Assert.Empty(r.FieldErrors);
    }

    [Fact]
    public void Failure_carries_code_message_and_field_errors()
    {
        var fields = new Dictionary<string, string> { ["Email"] = "Format email tidak valid" };
        var r = Result.Failure(ErrorCode.ValidationFailed, "Periksa isian", fields);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.ValidationFailed, r.Error);
        Assert.Equal("Periksa isian", r.Message);
        Assert.Equal("Format email tidak valid", r.FieldErrors["Email"]);
    }

    [Fact]
    public void Generic_success_returns_value()
    {
        var r = Result<int>.Success(42);
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Generic_failure_has_default_value()
    {
        var r = Result<string>.Failure(ErrorCode.NotFound, "Tidak ditemukan");
        Assert.False(r.IsSuccess);
        Assert.Null(r.Value);
        Assert.Equal(ErrorCode.NotFound, r.Error);
    }
}
