using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Schuly.Domain;

namespace Schuly.Infrastructure.Seeding
{
    /// <summary>
    /// Seeds the school-systems catalog from configuration. Seed-if-missing by
    /// <see cref="SchoolSystem.Key"/>: config supplies sensible defaults on a fresh
    /// database and new entries get added over time, but anything an administrator
    /// edits afterwards is left untouched.
    /// </summary>
    public static class SchoolSystemSeeder
    {
        public const string ConfigSection = "SchoolSystems";

        public static async Task SeedAsync(SchulyDbContext dbContext, IConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var seeds = configuration.GetSection(ConfigSection).Get<List<SchoolSystemSeed>>();
            if (seeds is null || seeds.Count == 0)
                return;

            var existingKeys = await dbContext.SchoolSystems
                .Select(s => s.Key)
                .ToListAsync(cancellationToken);

            var missing = seeds
                .Where(s => !string.IsNullOrWhiteSpace(s.Key) && !existingKeys.Contains(s.Key))
                .Select(s => new SchoolSystem
                {
                    Key = s.Key,
                    DisplayName = s.DisplayName,
                    LoginMethod = s.LoginMethod,
                    LogoUrl = s.LogoUrl,
                    SchulwareApiBaseUrl = s.SchulwareApiBaseUrl,
                    Enabled = s.Enabled,
                    SortOrder = s.SortOrder,
                    LoginFields = s.LoginFields.Select(f => new SchoolSystemLoginField
                    {
                        Key = f.Key,
                        Label = f.Label,
                        Type = f.Type,
                        Placeholder = f.Placeholder,
                        DefaultValue = f.DefaultValue,
                        Required = f.Required
                    }).ToList()
                })
                .ToList();

            if (missing.Count == 0)
                return;

            await dbContext.SchoolSystems.AddRangeAsync(missing, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public class SchoolSystemSeed
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string LoginMethod { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? SchulwareApiBaseUrl { get; set; }
        public bool Enabled { get; set; } = true;
        public int SortOrder { get; set; }
        public List<SchoolSystemLoginFieldSeed> LoginFields { get; set; } = [];
    }

    public class SchoolSystemLoginFieldSeed
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string? Placeholder { get; set; }
        public string? DefaultValue { get; set; }
        public bool Required { get; set; } = true;
    }
}
