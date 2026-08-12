using Microsoft.EntityFrameworkCore;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Seeding;

namespace Schuly.API.Extensions
{
    public static class MigrationExtensions
    {
        public static WebApplication ApplyMigrations(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            db.Database.Migrate();
            return app;
        }

        public static async Task<WebApplication> SeedSchoolSystemsAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            await SchoolSystemSeeder.SeedAsync(db, configuration);
            return app;
        }
    }
}
