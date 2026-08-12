using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Schuly.API.Plugins;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Extensions
{
    public static class PluginExtensions
    {
        private const string DefaultRegistry = "https://raw.githubusercontent.com/schulydev/SchulyPlugins/repo/";

        public static IServiceCollection AddSchulyPlugins(this IServiceCollection services, IConfiguration configuration, IMvcBuilder mvc)
        {
            services.AddSingleton<PluginAssemblyMap>();
            services.AddSingleton<PluginEndpointDataSource>();
            services.AddSingleton<IActionDescriptorChangeProvider>(PluginActionDescriptorChangeProvider.Instance);

            services.AddSingleton(_ => new PluginStore(ResolvePath(configuration["Plugins:Directory"] ?? "plugins")));
            services.AddSingleton(_ => new PluginSet(ResolvePath(configuration["Plugins:File"] ?? "plugins.yml")));
            services.AddSingleton(sp => new PluginRegistryClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("plugin-registry"),
                configuration["Plugins:Registry"] ?? DefaultRegistry));

            services.AddSingleton<PluginHost>();
            services.AddSingleton<PluginManager>();

            // Tag plugin controllers (added as ApplicationParts at runtime) with their
            // owning plugin so the scope middleware routes requests into the right scope.
            services.AddSingleton<IConfigureOptions<MvcOptions>, PluginMvcConfigureOptions>();

            services.AddTransient<IReadOnlyList<ISchulyPlugin>>(sp =>
                sp.GetRequiredService<PluginHost>().Instances());

            _ = mvc; // reserved: plugin controllers are added as ApplicationParts at runtime
            return services;
        }

        public static async Task<WebApplication> UseSchulyPluginsAsync(this WebApplication app)
        {
            ((IEndpointRouteBuilder)app).DataSources.Add(app.Services.GetRequiredService<PluginEndpointDataSource>());
            await app.Services.GetRequiredService<PluginManager>().ReconcileAsync();
            return app;
        }

        private static string ResolvePath(string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

        private sealed class PluginMvcConfigureOptions(PluginAssemblyMap map) : IConfigureOptions<MvcOptions>
        {
            public void Configure(MvcOptions options) =>
                options.Conventions.Add(new PluginControllerConvention(map));
        }
    }
}
