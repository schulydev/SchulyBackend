using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schuly.API.Extensions;
using Schuly.API.Plugins;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    /// <summary>
    /// Proves the core hot-swap promise end-to-end over HTTP: a plugin's minimal-API
    /// endpoint AND its MVC controller (resolving a service from the plugin's own child
    /// container) appear when the plugin is loaded at runtime and 404 after it's
    /// unloaded — no process restart.
    /// </summary>
    public class PluginHotSwapTests
    {
        [Test]
        public async Task Endpoints_and_controllers_hot_load_and_unload()
        {
            await using var h = await Harness.StartAsync();

            // Nothing loaded yet.
            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(await h.Status("/api/plugins/test/controller-ping")).IsEqualTo(HttpStatusCode.NotFound);

            // Hot-load the plugin.
            await h.Host.LoadAsync(h.CopyTestPlugin(), h.Directory);

            var ping = await h.Client.GetAsync("/api/plugins/test/ping");
            await Assert.That(ping.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await ping.Content.ReadAsStringAsync()).Contains("pong");

            // Controller resolves TestGreeter from the plugin's child container.
            var ctrl = await h.Client.GetAsync("/api/plugins/test/controller-ping");
            await Assert.That(ctrl.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await ctrl.Content.ReadAsStringAsync()).Contains("child-di-ok");

            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsTrue();

            // Hot-unload — both surfaces disappear.
            await h.Host.UnloadAsync("Test Plugin");
            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(await h.Status("/api/plugins/test/controller-ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsFalse();
        }

        private sealed class Harness(WebApplication app, HttpClient client, PluginHost host, string directory)
            : IAsyncDisposable
        {
            public HttpClient Client { get; } = client;
            public PluginHost Host { get; } = host;
            public string Directory { get; } = directory;

            public static async Task<Harness> StartAsync()
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
                builder.Services.AddSchulyVault();
                builder.Services.AddScoped<IPluginUserContext, FakePluginUserContext>();
                builder.Services.AddAuthorization();
                builder.Services.AddSingleton<Schuly.API.Services.PluginSchedulerRegistry>();
                var mvc = builder.Services.AddControllers();
                builder.Services.AddSchulyPlugins(builder.Configuration, mvc);

                var app = builder.Build();
                app.UseRouting();
                app.UseAuthorization();
                app.UseMiddleware<PluginScopeMiddleware>();
                app.MapControllers();
                await app.UseSchulyPluginsAsync();
                await app.StartAsync();

                return new Harness(app, app.GetTestClient(), app.Services.GetRequiredService<PluginHost>(), dir);
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
}
