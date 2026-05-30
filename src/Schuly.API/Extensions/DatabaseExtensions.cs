using Microsoft.EntityFrameworkCore;
using Schuly.Infrastructure;

namespace Schuly.API.Extensions
{
    public static class DatabaseExtensions
    {
        public static IServiceCollection AddSchulyDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<SchulyDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("SchulyDatabase"),
                    npgsqlOptions => npgsqlOptions
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null
                        )
                ));
            return services;
        }
    }
}
