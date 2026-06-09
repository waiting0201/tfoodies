using System.Text.Json.Serialization;

namespace TFoodies.Contracts.Common;

public class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data) => new() { Data = data };
}

public class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public ApiError Error { get; set; } = null!;

    public static ApiErrorResponse Create(string code, string message, string? details = null)
        => new() { Error = new ApiError { Code = code, Message = message, Details = details } };
}

public class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}
