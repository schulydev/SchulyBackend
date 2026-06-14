using Microsoft.AspNetCore.Diagnostics;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Maps the role gate's <see cref="UnauthorizedAccessException"/> (thrown by
    /// the authorization pipeline behavior) to HTTP 403 instead of a bare 500.
    /// Any other exception falls through to the default ProblemDetails handler.
    /// </summary>
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

    public static class ExceptionHandlingExtensions
    {
        public static IServiceCollection AddSchulyExceptionHandling(this IServiceCollection services)
        {
            services.AddExceptionHandler<UnauthorizedExceptionHandler>();
            services.AddProblemDetails();
            return services;
        }
    }
}
