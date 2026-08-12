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

    public sealed class TestPlugin : ISchulyPlugin
    {
        public const string PluginName = "Test Plugin";

        public string Name => PluginName;
        public string Version => "1.0.0";

        public void ConfigureServices(IServiceCollection services, PluginServiceContext context) =>
            services.AddScoped<TestGreeter>();

        public void ConfigureEndpoints(IEndpointRouteBuilder endpoints) =>
            endpoints.MapGet("/api/plugins/test/ping", () => Results.Ok("pong")).AllowAnonymous();

        public Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [ApiController]
    [Route("api/plugins/test")]
    public sealed class TestPluginController(TestGreeter greeter) : ControllerBase
    {
        [HttpGet("controller-ping")]
        public IActionResult Ping() => Ok(greeter.Greet());
    }
}
