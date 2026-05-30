using Microsoft.AspNetCore.HttpLogging;

namespace Schuly.API.Extensions
{
    public static class RequestLoggingExtensions
    {
        // HTTP request logging — surfaces method, path, status, and body (e.g. 400
        // reason) for every request. Caller decides when to enable it (dev only).
        public static IServiceCollection AddSchulyRequestLogging(this IServiceCollection services)
        {
            services.AddHttpLogging(o =>
            {
                o.LoggingFields = HttpLoggingFields.RequestPath
                                | HttpLoggingFields.RequestMethod
                                | HttpLoggingFields.RequestQuery
                                | HttpLoggingFields.ResponseStatusCode
                                | HttpLoggingFields.ResponseBody;
                o.ResponseBodyLogLimit = 2048;
            });
            return services;
        }
    }
}
