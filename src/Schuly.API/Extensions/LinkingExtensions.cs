using Schuly.API.Linking;

namespace Schuly.API.Extensions
{
    public static class LinkingExtensions
    {
        public static IServiceCollection AddSchulyLinking(this IServiceCollection services)
        {
            services.AddDataProtection();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<LinkSessionStore>();
            services.AddHostedService<LinkSessionSweeper>();
            services.AddSingleton<LinkingService>();
            return services;
        }
    }
}
