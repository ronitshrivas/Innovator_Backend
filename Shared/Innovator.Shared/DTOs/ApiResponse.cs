namespace Innovator.Shared.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public class PaginatedResponse<T>
{
    public List<T> Results { get; set; } = new();
    public int Count { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNext => Page * PageSize < Count;
}
