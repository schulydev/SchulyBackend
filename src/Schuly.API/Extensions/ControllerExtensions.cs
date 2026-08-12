using System.Text.Json.Serialization;

namespace Schuly.API.Extensions
{
    public static class ControllerExtensions
    {
        public static IMvcBuilder AddSchulyControllers(this IServiceCollection services)
        {
            services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            return services.AddControllers(options =>
                    options.Conventions.Add(new HidePluginEndpointsConvention()))
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        }
    }
}
