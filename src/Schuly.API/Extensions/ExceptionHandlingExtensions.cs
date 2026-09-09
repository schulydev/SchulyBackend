using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Schuly.API.Extensions
{
    public sealed class UnauthorizedExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not UnauthorizedAccessException)
                return false;

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { error = exception.Message }, cancellationToken);
            return true;
        }
    }

    // Backstop for write paths that have no explicit pre-check: maps constraint
    // violations that reach the database to a 409/400 instead of a bare 500.
    public sealed class DbUpdateExceptionHandler(ILogger<DbUpdateExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not DbUpdateException { InnerException: PostgresException pgException })
                return false;

            int statusCode;
            string error;

            switch (pgException.SqlState)
            {
                case PostgresErrorCodes.UniqueViolation:
                    statusCode = StatusCodes.Status409Conflict;
                    error = "The request conflicts with an existing record";
                    break;
                case PostgresErrorCodes.ForeignKeyViolation:
                    statusCode = StatusCodes.Status400BadRequest;
                    error = "A referenced record does not exist";
                    break;
                case PostgresErrorCodes.CheckViolation:
                case PostgresErrorCodes.NotNullViolation:
                    statusCode = StatusCodes.Status400BadRequest;
                    error = "The request violates a database constraint";
                    break;
                default:
                    return false;
            }

            logger.LogWarning("Database constraint {ConstraintName} violated with SQL state {SqlState}", pgException.ConstraintName, pgException.SqlState);

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(new { error }, cancellationToken);
            return true;
        }
    }

    public static class ExceptionHandlingExtensions
    {
        public static IServiceCollection AddSchulyExceptionHandling(this IServiceCollection services)
        {
            services.AddExceptionHandler<UnauthorizedExceptionHandler>();
            services.AddExceptionHandler<DbUpdateExceptionHandler>();
            services.AddProblemDetails();
            return services;
        }
    }
}
