using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Schuly.API.Extensions;
using Schuly.Infrastructure;

namespace Schuly.API
{
    // dotnet-ef prefers a design-time factory over resolving the app's host, so this has to
    // register TickerQ the same way Program.cs does or the scaffolded model loses the ticker
    // tables. The connection string is irrelevant to scaffolding.
    public sealed class SchulyDbContextFactory : IDesignTimeDbContextFactory<SchulyDbContext>
    {
        public SchulyDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();
            services.AddDbContext<SchulyDbContext>(options => options.UseNpgsql());
            services.AddSchulyTickerQ();

            return services.BuildServiceProvider().GetRequiredService<SchulyDbContext>();
        }
    }
}
