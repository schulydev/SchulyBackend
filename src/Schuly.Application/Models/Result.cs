namespace Schuly.Application.Models
{
    /// <summary>How a <see cref="Result"/> should surface — maps to an HTTP status at the edge.</summary>
    public enum ResultStatus
    {
        Ok,
        Error,      // 400
        Forbidden,  // 403
        Conflict,   // 409
    }

    public class Result
    {
        private Result(ResultStatus status, string? error)
        {
            Status = status;
            Error = error;
        }

        public ResultStatus Status { get; }
        public bool IsSuccess => Status == ResultStatus.Ok;
        public bool IsFailure => !IsSuccess;
        public string? Error { get; }

        public static Result Success() => new(ResultStatus.Ok, null);
        public static Result Failure(string error) => new(ResultStatus.Error, error);
        public static Result Forbidden(string error = "Forbidden") => new(ResultStatus.Forbidden, error);
        public static Result Conflict(string error) => new(ResultStatus.Conflict, error);

        public static Result<T> Success<T>(T value) => Result<T>.Success(value);
        public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
        public static Result<T> Forbidden<T>(string error = "Forbidden") => Result<T>.Forbidden(error);
        public static Result<T> Conflict<T>(string error) => Result<T>.Conflict(error);
    }

    public class Result<T>
    {
        private Result(ResultStatus status, T? value, string? error)
        {
            Status = status;
            Value = value;
            Error = error;
        }

        public ResultStatus Status { get; }
        public bool IsSuccess => Status == ResultStatus.Ok;
        public bool IsFailure => !IsSuccess;
        public T? Value { get; }
        public string? Error { get; }

        public static Result<T> Success(T value) => new(ResultStatus.Ok, value, null);
        public static Result<T> Failure(string error) => new(ResultStatus.Error, default, error);
        public static Result<T> Forbidden(string error = "Forbidden") => new(ResultStatus.Forbidden, default, error);
        public static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
    }
}
