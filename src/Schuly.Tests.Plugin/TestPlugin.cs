using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Schuly.Plugin.Abstractions;

namespace Schuly.Tests.Plugin
{
    public sealed class TestGreeter
    {
        public string Greet() => "child-di-ok";
    }

    public sealed class TestBackgroundTask : IPluginBackgroundTask
    {
        public string Name => "test.sync";
        public PluginSchedule Schedule => PluginSchedule.Every(TimeSpan.FromMinutes(30));
        public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class TestEventHandler(TestGreeter greeter) : IPluginEventHandler<string>
    {
        public Task HandleAsync(string path, CancellationToken cancellationToken = default) =>
            File.WriteAllTextAsync(path, greeter.Greet(), cancellationToken);
    }

    /// <summary>
    /// Leaves a file behind when the child container that built it is disposed, so a test
    /// on the host side can tell whether a failed load actually tore its provider down.
    /// </summary>
    public sealed class DisposeProbe(string markerPath) : IDisposable
    {
        public void Dispose() => File.WriteAllText(markerPath, "disposed");
    }

    public sealed class TestPlugin : ISchulyPlugin
    {
        public const string PluginName = "Test Plugin";
        public const string FailMigrateMarker = "fail-migrate";
        public const string ProviderDisposedMarker = "provider-disposed";

        private string? _pluginDirectory;

        public string Name => PluginName;
        public string Version => "1.0.0";

        public void ConfigureServices(IServiceCollection services, PluginServiceContext context)
        {
            var directory = context.Configuration["Plugins:Directory"];
            _pluginDirectory = directory;
            services.AddScoped<TestGreeter>();
            services.AddSingleton<IPluginBackgroundTask, TestBackgroundTask>();
            services.AddScoped<IPluginEventHandler<string>, TestEventHandler>();
            // Factory-registered, not instance-registered: the container only disposes what
            // it built itself, and the probe is worthless unless the container owns it.
            if (directory is not null)
                services.AddSingleton(_ => new DisposeProbe(Path.Combine(directory, ProviderDisposedMarker)));
        }

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapGet("/api/plugins/test/ping", () => Results.Ok("pong")).AllowAnonymous();

        public Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (_pluginDirectory is null || !File.Exists(Path.Combine(_pluginDirectory, FailMigrateMarker)))
                return Task.CompletedTask;

            // Touch the probe so the child container owns it, then fail the load: the host
            // must dispose the provider on the way out, which writes the marker.
            serviceProvider.GetRequiredService<DisposeProbe>();
            throw new InvalidOperationException("migrate failed");
        }
    }

    [ApiController]
    [Route("api/plugins/test")]
    public sealed class TestPluginController(TestGreeter greeter) : ControllerBase
    {
        [HttpGet("controller-ping")]
        public IActionResult Ping() => Ok(greeter.Greet());
    }
}
