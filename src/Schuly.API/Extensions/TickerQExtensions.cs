using Schuly.Infrastructure;
using TickerQ.Dashboard.DependencyInjection;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.Customizer;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace Schuly.API.Extensions
{
    public static class TickerQExtensions
    {
        // Shared by Program.cs and the design-time DbContext factory: the model customizer
        // is what puts the ticker tables into SchulyDbContext's model, so scaffolding a
        // migration against a context registered without it silently drops them.
        public static IServiceCollection AddSchulyTickerQ(this IServiceCollection services, bool enableDashboard = false)
        {
            services.AddTickerQ(options =>
            {
                options.AddOperationalStore(ef => ef.UseApplicationDbContext<SchulyDbContext>(ConfigurationType.UseModelCustomizer));
                if (enableDashboard)
                    options.AddDashboard(dashboard => dashboard.SetBasePath("/tickerq"));
            });
            return services;
        }
    }
}
