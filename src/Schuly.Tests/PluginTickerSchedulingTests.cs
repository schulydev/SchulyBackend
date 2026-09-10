using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schuly.API.Extensions;
using Schuly.API.Plugins;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;
using Schuly.Tests.TestHelpers;
using TickerQ.DependencyInjection;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;

namespace Schuly.Tests
{
    // Boots a real host with the real TickerQ services and the real TickerQPluginTaskScheduler
    // (no FakePluginTaskScheduler) to reproduce the production ordering bug: plugins load - and
    // ask the scheduler to write tickers - via UseSchulyPluginsAsync() before the host starts,
    // which is before TickerQ's own initializer has built its [TickerFunction] registry. This is
    // the one test that exercises that ordering end to end, so it has to run alone: like
    // Host_ticker_function_is_registered_by_the_source_generator, it touches the same
    // process-wide static TickerFunctionProvider state.
    [NotInParallel]
    public class PluginTickerSchedulingTests
    {
        [Test]
        public async Task Plugin_load_before_host_start_still_schedules_its_ticker()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"schuly-ticker-plugins-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Only read by PluginHost to derive a per-plugin connection string; the
                // actual SchulyDbContext below runs on the EF InMemory provider.
                ["ConnectionStrings:SchulyDatabase"] = "Host=localhost;Database=schuly_test;Username=x;Password=y",
                ["Plugins:Directory"] = dir,
                ["Plugins:File"] = Path.Combine(dir, "plugins.yml"),
            });

            // Every DbContext instance must share this one name - it's read inside the options
            // lambda on each scope's resolution, so a Guid generated there would hand out a
            // fresh, empty database per scope instead of one shared store for the whole test.
            var databaseName = $"schuly-ticker-{Guid.NewGuid():N}";
            builder.Services.AddDbContext<SchulyDbContext>(options => options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            builder.Services.AddSchulyTickerQ();

            builder.Services.AddHttpClient();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            builder.Services.AddSchulyVault(builder.Configuration, isDevelopment: true);
            builder.Services.AddScoped<IPluginUserContext, FakePluginUserContext>();
            builder.Services.AddAuthorization();

            builder.Services.AddSingleton<TickerQPluginTaskScheduler>();
            builder.Services.AddSingleton<IPluginTaskScheduler>(sp => sp.GetRequiredService<TickerQPluginTaskScheduler>());
            builder.Services.AddHostedService(sp => sp.GetRequiredService<TickerQPluginTaskScheduler>());
            builder.Services.AddSingleton<PluginTaskRunner>();

            var mvc = builder.Services.AddControllers();
            builder.Services.AddSchulyPlugins(builder.Configuration, mvc);

            var app = builder.Build();

            try
            {
                await app.UseSchulyPluginsAsync();

                // Reproduce the production path: the plugin (and its scheduled ticker write)
                // must load before the host - and TickerQ's own initializer - starts.
                var host = app.Services.GetRequiredService<PluginHost>();
                const string dll = "Schuly.Tests.Plugin.dll";
                File.Copy(Path.Combine(AppContext.BaseDirectory, dll), Path.Combine(dir, dll), overwrite: true);
                var manifest = new PluginManifest { Name = "Test Plugin", Version = "1.0.0", Dll = dll, Files = [dll] };
                await host.LoadAsync(manifest, dir);

                app.UseTickerQ();
                await app.StartAsync();

                await Assert.That(TickerFunctionProvider.TickerFunctions.ContainsKey("RunPluginTask")).IsTrue();

                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                var ticker = await db.Set<CronTickerEntity>().FirstOrDefaultAsync(t => t.Function == "RunPluginTask" && t.Description == "Test Plugin/test.sync");

                await Assert.That(ticker).IsNotNull();
                await Assert.That(ticker!.Expression).IsEqualTo("0 */30 * * * *");

                var scheduler = app.Services.GetRequiredService<TickerQPluginTaskScheduler>();
                var snapshot = await scheduler.SnapshotAsync();
                var status = snapshot.Where(s => s.Plugin == "Test Plugin" && s.Name == "test.sync").ToList();

                await Assert.That(status.Count).IsEqualTo(1);
                await Assert.That(status[0].Cron).IsEqualTo("*/30 * * * *");
                await Assert.That(status[0].IntervalSeconds).IsEqualTo(1800);
                await Assert.That(status[0].NextRunAt).IsNotNull();
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }
    }
}
