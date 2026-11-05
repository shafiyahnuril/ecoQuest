using System;
using EcoQuest.Core.Common;

namespace EcoQuest.Data.Errors;

public sealed class DataAccessException(ErrorCode code, string operation, Exception? inner = null)
    : Exception($"{operation} gagal: {code}", inner)
{
    public ErrorCode Code { get; } = code;
    public string Operation { get; } = operation;
}
