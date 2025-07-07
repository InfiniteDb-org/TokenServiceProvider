namespace TokenService.Api.Models;

// Base class for all API responses
public abstract class ResponseResult
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
}

// Simple non-generic response for errors etc
public class SimpleResponseResult : ResponseResult
{
}

// Generic response for data
public class ResponseResult<T> : ResponseResult
{
    public T? Data { get; set; }

    public static ResponseResult<T> Success(T data, string message = "Operation completed successfully")
    {
        return new ResponseResult<T>
        {
            Succeeded = true,
            Message = message,
            Data = data
        };
    }

    public static ResponseResult<T> Failure(string message)
    {
        return new ResponseResult<T>
        {
            Succeeded = false,
            Message = message,
            Data = default
        };
    }
}