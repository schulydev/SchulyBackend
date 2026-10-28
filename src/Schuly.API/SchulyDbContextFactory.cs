using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.API
{
    // dotnet-ef prefers a design-time factory over resolving the app's host, so this has to
    // register everything Program.cs registers for the DbContext - the notification outbox
    // SaveChanges interceptor - or the scaffolded model can drift from what the app actually runs with. The connection
    // string is irrelevant to scaffolding.
    public sealed class SchulyDbContextFactory : IDesignTimeDbContextFactory<SchulyDbContext>
    {
        public SchulyDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<INotificationOriginContext, NotificationOriginContext>();
            services.AddScoped<NotificationOutboxInterceptor>();
            services.AddDbContext<SchulyDbContext>((sp, options) =>
                options.UseNpgsql().AddInterceptors(sp.GetRequiredService<NotificationOutboxInterceptor>()));

            return services.BuildServiceProvider().GetRequiredService<SchulyDbContext>();
        }
    }
}
