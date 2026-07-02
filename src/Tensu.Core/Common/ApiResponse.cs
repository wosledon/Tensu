using System.Text.Json.Serialization;

namespace Tensu.Core.Common;

public class ApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "success";

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data) => new() { Code = 0, Message = "success", Data = data };
    public static ApiResponse<T> Error(int code, string message) => new() { Code = code, Message = message, Data = default };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success() => new() { Code = 0, Message = "success", Data = null };
    public new static ApiResponse Error(int code, string message) => new() { Code = code, Message = message, Data = null };
}
