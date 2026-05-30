using System.Text.Json.Serialization;

namespace Schuly.API.Extensions
{
    public static class ControllerExtensions
    {
        // Controllers with enums serialized as strings (matches the OpenAPI document
        // and the generated Dart client).
        public static IMvcBuilder AddSchulyControllers(this IServiceCollection services)
        {
            return services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }
    }
}
