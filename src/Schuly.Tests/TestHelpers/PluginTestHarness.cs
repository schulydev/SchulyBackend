using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schuly.API.Extensions;
using Schuly.API.Plugins;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;

namespace Schuly.Tests.TestHelpers
{
    // Shared TestServer harness for plugin host tests: spins up a minimal app with the
    // plugin pipeline wired in and a FakePluginTaskScheduler standing in for TickerQ.
    internal sealed class PluginTestHarness(WebApplication app, HttpClient client, PluginHost host, FakePluginTaskScheduler scheduler, string directory)
        : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public PluginHost Host { get; } = host;
        public FakePluginTaskScheduler Scheduler { get; } = scheduler;
        public string Directory { get; } = directory;
        public IServiceProvider Services => app.Services;

        public static async Task<PluginTestHarness> StartAsync()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"schuly-plugins-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(dir);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SchulyDatabase"] = "Host=localhost;Database=schuly_test;Username=x;Password=y",
                ["Plugins:Directory"] = dir,
                ["Plugins:File"] = Path.Combine(dir, "plugins.yml"),
            });

            builder.Services.AddHttpClient();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            builder.Services.AddSchulyVault(builder.Configuration, isDevelopment: true);
            builder.Services.AddScoped<IPluginUserContext, FakePluginUserContext>();
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<FakePluginTaskScheduler>();
            builder.Services.AddSingleton<IPluginTaskScheduler>(sp => sp.GetRequiredService<FakePluginTaskScheduler>());
            var mvc = builder.Services.AddControllers();
            builder.Services.AddSchulyPlugins(builder.Configuration, mvc);

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthorization();
            app.UseMiddleware<PluginScopeMiddleware>();
            app.MapControllers();
            await app.UseSchulyPluginsAsync();
            await app.StartAsync();

            return new PluginTestHarness(app, app.GetTestClient(), app.Services.GetRequiredService<PluginHost>(), app.Services.GetRequiredService<FakePluginTaskScheduler>(), dir);
        }

        public PluginManifest CopyTestPlugin()
        {
            const string dll = "Schuly.Tests.Plugin.dll";
            File.Copy(Path.Combine(AppContext.BaseDirectory, dll), Path.Combine(Directory, dll), overwrite: true);
            return new PluginManifest { Name = "Test Plugin", Version = "1.0.0", Dll = dll, Files = [dll] };
        }

        public async Task<HttpStatusCode> Status(string url) => (await Client.GetAsync(url)).StatusCode;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Client.Dispose();
            try { System.IO.Directory.Delete(Directory, recursive: true); } catch { /* best effort */ }
        }
    }
}
