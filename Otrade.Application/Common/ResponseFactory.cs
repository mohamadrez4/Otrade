namespace Otrade.Application.Common;

public static class ResponseFactory
{
    public static ApiResponse<T> Success<T>(
        T data,
        string message = "Operation completed successfully.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail<T>(
        string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default
        };
    }
}