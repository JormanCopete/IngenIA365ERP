namespace IngenIA365ERP.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default!, false, error);
}

public class Result<T> : Result
{
    public T Value { get; }

    internal Result(T value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        Value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "Value cannot be null");
    public static readonly Error NotFound = new("Error.NotFound", "Resource not found");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "Not authorized");
    public static readonly Error Forbidden = new("Error.Forbidden", "Access forbidden");
    public static readonly Error Conflict = new("Error.Conflict", "Resource conflict");
    public static readonly Error Validation = new("Error.Validation", "Validation failed");
}
