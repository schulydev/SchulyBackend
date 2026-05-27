using Microsoft.Extensions.DependencyInjection;
using Schuly.Plugin.Abstractions;
using System.Reflection;

namespace Schuly.API.Extensions
{
    public static class PluginExtensions
    {
        /// <summary>
        /// Discovers plugins under <c>plugins/</c>, lets each one register its
        /// services, and registers each plugin's assembly as an MVC
        /// <see cref="ApplicationPart"/>. That registration is what lets plugin
        /// authors choose: minimal-API endpoints via <c>ConfigureEndpoints</c>,
        /// or a regular ASP.NET Controller in the plugin DLL — both appear in
        /// Swagger.
        /// </summary>
        public static IServiceCollection AddPlugins(
            this IServiceCollection services, IConfiguration configuration, IMvcBuilder mvcBuilder)
        {
            var plugins = DiscoverPlugins(configuration);
            var mainConnectionString = configuration.GetConnectionString("SchulyDatabase")
                ?? throw new InvalidOperationException("SchulyDatabase connection string not configured");

            var pluginsConfigDir = configuration["Plugins:ConfigDirectory"] ?? "plugins-config";
            if (!Path.IsPathRooted(pluginsConfigDir))
                pluginsConfigDir = Path.Combine(AppContext.BaseDirectory, pluginsConfigDir);

            foreach (var plugin in plugins)
            {
                var pluginDbName = $"schuly_plugin_{plugin.Name.ToLowerInvariant().Replace(" ", "_")}";
                var pluginConnectionString = ReplaceDatabase(mainConnectionString, pluginDbName);
                var pluginConfig = LoadPluginConfig(plugin, pluginsConfigDir);
                var context = new PluginServiceContext(pluginConnectionString, pluginConfig);

                plugin.ConfigureServices(services, context);
                mvcBuilder.AddApplicationPart(plugin.GetType().Assembly);
            }

            services.AddSingleton<IReadOnlyList<ISchulyPlugin>>(plugins);

            return services;
        }

        public static async Task<WebApplication> UsePluginsAsync(this WebApplication app)
        {
            var plugins = app.Services.GetRequiredService<IReadOnlyList<ISchulyPlugin>>();

            foreach (var plugin in plugins)
            {
                await plugin.MigrateAsync(app.Services);
                plugin.ConfigureEndpoints(app);
                app.Logger.LogInformation("Loaded plugin: {Name} v{Version}", plugin.Name, plugin.Version);
            }

            return app;
        }

        /// <summary>
        /// Per-plugin config lives at <c>{configDir}/{PluginAssemblyName}.yml</c>
        /// (e.g. <c>plugins-config/Schuly.Plugin.Schulware.yml</c>). Keeps deployment-
        /// specific config out of the plugin drop folder so DLLs can be replaced
        /// without touching config — and so a single config dir can be mounted as a
        /// volume / ConfigMap.
        /// </summary>
        private static IConfiguration LoadPluginConfig(ISchulyPlugin plugin, string configDir)
        {
            var assemblyName = plugin.GetType().Assembly.GetName().Name ?? plugin.Name;
            var ymlPath = Path.Combine(configDir, $"{assemblyName}.yml");
            var yamlPath = Path.Combine(configDir, $"{assemblyName}.yaml");

            var builder = new ConfigurationBuilder();

            if (File.Exists(ymlPath))
                builder.AddYamlFile(ymlPath, optional: true, reloadOnChange: true);
            else if (File.Exists(yamlPath))
                builder.AddYamlFile(yamlPath, optional: true, reloadOnChange: true);

            // Env var overlay: SCHULY_PLUGIN_<NAME>__Section__Key=value (double underscore for nesting)
            builder.AddEnvironmentVariables($"SCHULY_PLUGIN_{plugin.Name.ToUpperInvariant().Replace(" ", "_")}_");

            return builder.Build();
        }

        private static string ReplaceDatabase(string connectionString, string newDatabase)
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = newDatabase
            };
            return builder.ConnectionString;
        }

        private static List<ISchulyPlugin> DiscoverPlugins(IConfiguration configuration)
        {
            var plugins = new List<ISchulyPlugin>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(ISchulyPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is ISchulyPlugin plugin)
                            plugins.Add(plugin);
                    }
                }
                catch { }
            }

            var pluginDir = configuration["Plugins:Directory"] ?? "plugins";

            if (!Path.IsPathRooted(pluginDir))
                pluginDir = Path.Combine(AppContext.BaseDirectory, pluginDir);

            if (!Directory.Exists(pluginDir))
                return plugins;

            foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(ISchulyPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

                    foreach (var type in pluginTypes)
                    {
                        if (Activator.CreateInstance(type) is ISchulyPlugin plugin)
                            plugins.Add(plugin);
                    }
                }
                catch (ReflectionTypeLoadException rex)
                {
                    Console.Error.WriteLine($"[PluginDiscovery] {dll}: type-load failed ({rex.Message})");
                    foreach (var le in rex.LoaderExceptions.Take(5))
                        Console.Error.WriteLine($"    - {le?.Message}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[PluginDiscovery] {dll}: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException is not null)
                        Console.Error.WriteLine($"    inner: {ex.InnerException.Message}");
                }
            }

            return plugins;
        }
    }
}
