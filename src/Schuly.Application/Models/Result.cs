namespace Schuly.Application.Models
{
    public class Result
    {
        private Result(bool isSuccess, string? error, bool isForbidden = false)
        {
            IsSuccess = isSuccess;
            Error = error;
            IsForbidden = isForbidden;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        /// <summary>The caller is authenticated but not allowed this resource — maps to HTTP 403.</summary>
        public bool IsForbidden { get; }
        public string? Error { get; }

        public static Result Success() => new(true, null);
        public static Result Failure(string error) => new(false, error);
        public static Result Forbidden(string error = "Forbidden") => new(false, error, isForbidden: true);

        public static Result<T> Success<T>(T value) => Result<T>.Success(value);
        public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
        public static Result<T> Forbidden<T>(string error = "Forbidden") => Result<T>.Forbidden(error);
    }

    public class Result<T>
    {
        private Result(bool isSuccess, T? value, string? error, bool isForbidden = false)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
            IsForbidden = isForbidden;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public bool IsForbidden { get; }
        public T? Value { get; }
        public string? Error { get; }

        public static Result<T> Success(T value) => new(true, value, null);
        public static Result<T> Failure(string error) => new(false, default, error);
        public static Result<T> Forbidden(string error = "Forbidden") => new(false, default, error, isForbidden: true);
    }
}
