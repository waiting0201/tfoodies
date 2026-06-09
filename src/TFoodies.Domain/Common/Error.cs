namespace TFoodies.Domain.Common;

/// <summary>
/// A structured, expected failure. Replaces the legacy pattern of swallowing
/// exceptions into <c>IResult.Exception</c> (see reference/old tfoodies.Service/Result.cs).
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string what) => new("not_found", $"{what} 不存在");
    public static Error Validation(string message) => new("validation", message);
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Unauthorized(string message = "未授權") => new("unauthorized", message);
    public static Error Forbidden(string message = "權限不足") => new("forbidden", message);

    public override string ToString() => string.IsNullOrEmpty(Code) ? Message : $"[{Code}] {Message}";
}
