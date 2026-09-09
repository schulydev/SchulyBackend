using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Schuly.API.Extensions
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddSchulyRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var subject = context.User.Identity?.IsAuthenticated == true
                        ? context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        : null;

                    var partitionKey = subject is { Length: > 0 }
                        ? $"sub:{subject}"
                        : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) });
                });
            });

            return services;
        }
    }
}
