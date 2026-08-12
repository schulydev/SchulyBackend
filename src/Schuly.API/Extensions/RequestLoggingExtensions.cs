using Microsoft.AspNetCore.HttpLogging;

namespace Schuly.API.Extensions
{
    public static class RequestLoggingExtensions
    {
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
