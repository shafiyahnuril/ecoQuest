// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using System.Collections.Generic;
namespace EcoQuest.Core.Common;

public class Result
{
    public bool IsSuccess { get; }
    public ErrorCode Error { get; }
    public string? Message { get; }                                   // teks Bahasa Indonesia, siap tampil
    public IReadOnlyDictionary<string, string> FieldErrors { get; }   // key = nama property request (mis. "Email")

    protected Result(bool ok, ErrorCode error, string? message, IReadOnlyDictionary<string, string>? fieldErrors)
    { IsSuccess = ok; Error = error; Message = message; FieldErrors = fieldErrors ?? new Dictionary<string, string>(); }

    public static Result Success() => new(true, ErrorCode.None, null, null);
    public static Result Failure(ErrorCode error, string message, IReadOnlyDictionary<string, string>? fieldErrors = null)
        => new(false, error, message, fieldErrors);
}

public sealed class Result<T> : Result
{
    public T? Value { get; }
    private Result(bool ok, T? value, ErrorCode e, string? m, IReadOnlyDictionary<string, string>? f) : base(ok, e, m, f) => Value = value;
    public static Result<T> Success(T value) => new(true, value, ErrorCode.None, null, null);
    public static new Result<T> Failure(ErrorCode error, string message, IReadOnlyDictionary<string, string>? fieldErrors = null)
        => new(false, default, error, message, fieldErrors);
}
