namespace TaskManagement.Contracts.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? Error { get; private set; }
    public List<string> ValidationErrors { get; private set; } = new();

    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
    public static Result<T> ValidationFailure(List<string> errors) => new() 
    { 
        IsSuccess = false, 
        Error = "Validation failed", 
        ValidationErrors = errors 
    };
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? Error { get; private set; }
    public List<string> ValidationErrors { get; private set; } = new();

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error) => new() { IsSuccess = false, Error = error };
    public static Result ValidationFailure(List<string> errors) => new() 
    { 
        IsSuccess = false, 
        Error = "Validation failed", 
        ValidationErrors = errors 
    };
}
