using System.Text.Json.Serialization;

namespace Schuly.API.Extensions
{
    public static class ControllerExtensions
    {
        // Controllers with enums serialized as strings. The converter is registered on
        // both the MVC and the HTTP JSON options: controllers use the former, while
        // Microsoft.AspNetCore.OpenApi reads the latter for schema generation — without
        // it, enums are documented as bare integers and the client can't generate them.
        public static IMvcBuilder AddSchulyControllers(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            return services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }
    }
}
