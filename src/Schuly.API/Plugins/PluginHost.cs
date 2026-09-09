using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Schuly.Application.Abstractions;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Storage;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Plugins
{
    public sealed record LoadedPluginInfo(string Name, string Version);

    public sealed class PluginHost(IServiceProvider rootProvider, IConfiguration configuration, ApplicationPartManager partManager, PluginEndpointDataSource endpointSource, PluginAssemblyMap assemblyMap, IPluginTaskScheduler scheduler, ILogger<PluginHost> logger) : IPluginEventDispatcher
    {
        private readonly ConcurrentDictionary<string, LoadedPlugin> _loaded = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _gate = new(1, 1);

        public IServiceProvider? GetProvider(string pluginName) =>
            _loaded.TryGetValue(pluginName, out var p) ? p.Provider : null;

        public IReadOnlyList<LoadedPluginInfo> List() =>
            _loaded.Values.Select(p => new LoadedPluginInfo(p.Name, p.Version)).ToList();

        public IReadOnlyList<ISchulyPlugin> Instances() =>
            _loaded.Values.Select(p => p.Instance).ToList();

        public bool IsLoaded(string name) => _loaded.ContainsKey(name);

        public async Task<PluginLoginResult?> ConnectAsync(string systemKey, IReadOnlyDictionary<string, string> fields, string? displayName, CancellationToken ct = default)
        {
            foreach (var loaded in _loaded.Values)
            {
                using var scope = loaded.Provider.CreateScope();
                var login = scope.ServiceProvider.GetServices<IPluginLogin>()
                    .FirstOrDefault(l => string.Equals(l.SystemKey, systemKey, StringComparison.OrdinalIgnoreCase));
                if (login is null)
                    continue;

                return await login.ConnectAsync(fields, displayName, ct);
            }

            return null;
        }

        public async Task DispatchAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : notnull
        {
            if (_loaded.IsEmpty)
                return;

            foreach (var loaded in _loaded.Values)
            {
                try
                {
                    using var scope = loaded.Provider.CreateScope();
                    foreach (var handler in scope.ServiceProvider.GetServices<IPluginEventHandler<TEvent>>())
                    {
                        try
                        {
                            await handler.HandleAsync(message, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Plugin event handler {Handler} failed for {Event}", handler.GetType().Name, typeof(TEvent).Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Plugin {Name} failed to dispatch {Event}", loaded.Name, typeof(TEvent).Name);
                }
            }
        }

        private async Task SyncSchoolSystemsAsync(LoadedPlugin loaded, CancellationToken ct)
        {
            using var pluginScope = loaded.Provider.CreateScope();
            var descriptors = pluginScope.ServiceProvider.GetServices<IPluginLogin>()
                .Select(l => l.SchoolSystem)
                .Where(d => d is not null && !string.IsNullOrWhiteSpace(d.Key))
                .DistinctBy(d => d.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (descriptors.Count == 0)
                return;

            using var hostScope = rootProvider.CreateScope();
            var db = hostScope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            var existingKeys = await db.SchoolSystems.Select(s => s.Key).ToListAsync(ct);

            var missing = descriptors
                .Where(d => !existingKeys.Contains(d.Key))
                .Select(d => new SchoolSystem
                {
                    Key = d.Key,
                    DisplayName = d.DisplayName,
                    LoginMethod = d.LoginMethod,
                    LogoUrl = d.LogoUrl,
                    PrivateAuthStrategy = d.PrivateAuthStrategy,
                    StatelessBasePath = d.StatelessBasePath,
                    PluginBasePath = d.PluginBasePath,
                    Enabled = d.Enabled,
                    SortOrder = d.SortOrder,
                    LoginFields = d.LoginFields.Select(f => new SchoolSystemLoginField
                    {
                        Key = f.Key,
                        Label = f.Label,
                        Type = f.Type,
                        Placeholder = f.Placeholder,
                        DefaultValue = f.DefaultValue,
                        Required = f.Required,
                    }).ToList(),
                })
                .ToList();

            if (missing.Count == 0)
                return;

            await db.SchoolSystems.AddRangeAsync(missing, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} school system(s) from plugin {Name}", missing.Count, loaded.Name);
        }

        public async Task LoadAsync(PluginManifest manifest, string pluginDirectory, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var dllPath = Path.Combine(pluginDirectory, manifest.Dll);
                var alc = new PluginLoadContext(manifest.Name, pluginDirectory);
                // Load the main assembly from a byte copy so the DLL file isn't locked —
                // lets update/remove replace it while the old context is still unloading.
                var assembly = alc.LoadFromStream(new MemoryStream(await File.ReadAllBytesAsync(dllPath, ct)));

                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(ISchulyPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
                if (pluginType is null || Activator.CreateInstance(pluginType) is not ISchulyPlugin plugin)
                {
                    alc.Unload();
                    throw new InvalidOperationException($"No ISchulyPlugin found in {manifest.Dll}");
                }

                ServiceProvider? provider = null;
                AssemblyPart? part = null;
                LoadedPlugin? loaded = null;
                try
                {
                    var (builtProvider, pluginConfiguration) = BuildChildProvider(plugin);
                    provider = builtProvider;
                    await plugin.MigrateAsync(provider, ct);

                    var endpoints = PluginEndpointDataSource.Build(plugin.Name, rootProvider, plugin.ConfigureEndpoints);

                    part = new AssemblyPart(assembly);
                    partManager.ApplicationParts.Add(part);
                    assemblyMap.Add(assembly, plugin.Name);

                    loaded = new LoadedPlugin
                    {
                        Name = plugin.Name,
                        Version = plugin.Version,
                        Instance = plugin,
                        LoadContext = alc,
                        Assembly = assembly,
                        Provider = provider,
                        Configuration = pluginConfiguration,
                        Part = part,
                        Endpoints = endpoints,
                        TaskCts = new CancellationTokenSource(),
                    };
                    _loaded[plugin.Name] = loaded;

                    RefreshEndpoints();
                    PluginActionDescriptorChangeProvider.Instance.NotifyChanged();
                    await SyncBackgroundTasksAsync(loaded, ct);
                    await SyncSchoolSystemsAsync(loaded, ct);

                    logger.LogInformation("Loaded plugin {Name} v{Version}", plugin.Name, plugin.Version);
                }
                catch
                {
                    // A partial load must not leak the collectible ALC or the child provider
                    // (and its DbContext/HttpClient) — undo exactly what was already created,
                    // in reverse order, before letting the caller see the failure.
                    loaded?.TaskCts?.Cancel();
                    loaded?.TaskCts?.Dispose();
                    _loaded.TryRemove(plugin.Name, out _);
                    if (part is not null)
                    {
                        partManager.ApplicationParts.Remove(part);
                        assemblyMap.Remove(assembly);
                    }
                    PluginActionDescriptorChangeProvider.Instance.NotifyChanged();
                    RefreshEndpoints();
                    if (loaded is not null)
                        await scheduler.RemoveAsync(plugin.Name, CancellationToken.None);
                    if (provider is not null)
                        await provider.DisposeAsync();
                    alc.Unload();
                    throw;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task UnloadAsync(string name, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (!_loaded.TryRemove(name, out var plugin))
                    return;

                await scheduler.RemoveAsync(name, ct);
                plugin.TaskCts.Cancel();

                partManager.ApplicationParts.Remove(plugin.Part);
                assemblyMap.Remove(plugin.Assembly);
                PluginActionDescriptorChangeProvider.Instance.NotifyChanged();
                RefreshEndpoints();

                await WaitForInFlightRunsAsync(plugin);

                if (plugin.Instance is IAsyncDisposable instanceAsync)
                    await instanceAsync.DisposeAsync();
                else if (plugin.Instance is IDisposable instanceSync)
                    instanceSync.Dispose();

                await plugin.Provider.DisposeAsync();

                plugin.TaskCts.Dispose();
                plugin.LoadContext.Unload();

                for (var i = 0; i < 2; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                logger.LogInformation("Unloaded plugin {Name}", name);
            }
            finally
            {
                _gate.Release();
            }
        }

        private (ServiceProvider Provider, IConfiguration Configuration) BuildChildProvider(ISchulyPlugin plugin)
        {
            var services = new ServiceCollection();

            services.AddSingleton(rootProvider.GetRequiredService<ILoggerFactory>());
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton(rootProvider.GetRequiredService<IHttpClientFactory>());
            services.AddSingleton(rootProvider.GetRequiredService<IHttpContextAccessor>());
            services.AddSingleton(rootProvider.GetRequiredService<IPluginVaultFactory>());

            // The current request's user context, resolved lazily from the active
            // request scope so plugin code sees the real caller. Deferred (not resolved
            // at construction) so plugin services can also be built outside a request —
            // e.g. when the host reads a plugin's catalog descriptor at load time.
            services.AddScoped<IPluginUserContext>(_ => new DeferredPluginUserContext(rootProvider));

            services.AddKeyedSingleton<IPluginVault>(plugin.Name, (sp, _) =>
                sp.GetRequiredService<IPluginVaultFactory>().GetVault($"plugin:{plugin.Name}"));

            // Host-owned scoped services plugin code writes against — the main database
            // and blob storage. They're scoped, so they can't be forwarded as singletons:
            // each plugin scope gets its own host scope to resolve them from, disposed
            // with the plugin scope. Unlike the HttpContext bridge above this works in
            // both request (unified login) and background-task (sync) scopes.
            services.AddScoped(_ => new HostServiceScope(rootProvider));
            services.AddScoped(sp => sp.GetRequiredService<HostServiceScope>().Services.GetRequiredService<SchulyDbContext>());
            services.AddScoped(sp => sp.GetRequiredService<HostServiceScope>().Services.GetRequiredService<IDocumentStorage>());

            // Plugin services that inject IConfiguration see the host config with the
            // plugin's own plugins-config (yml + SCHULY_PLUGIN_* env) overlaid on top, so
            // a value in plugins-config/<assembly>.yml reaches the services that use it —
            // not just the load-time check that reads PluginServiceContext.Configuration.
            var pluginConfig = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddConfiguration(LoadPluginConfig(plugin))
                .Build();
            services.AddSingleton<IConfiguration>(pluginConfig);

            var context = new PluginServiceContext(PluginConnectionString(plugin.Name), pluginConfig);
            plugin.ConfigureServices(services, context);

            return (services.BuildServiceProvider(), pluginConfig);
        }

        private Task SyncBackgroundTasksAsync(LoadedPlugin plugin, CancellationToken ct)
        {
            var registrations = plugin.Provider.GetServices<IPluginBackgroundTask>()
                .Select(task => PluginTaskScheduleResolver.Resolve(plugin.Name, task, plugin.Configuration, logger))
                .ToList();

            return scheduler.SyncAsync(plugin.Name, registrations, ct);
        }

        public async Task RunTaskAsync(string pluginName, string taskName, CancellationToken cancellationToken = default)
        {
            if (!_loaded.TryGetValue(pluginName, out var plugin))
            {
                logger.LogWarning("Ticker fired for plugin task {Plugin}/{Task} but the plugin is not loaded", pluginName, taskName);
                return;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, plugin.TaskCts.Token);
            Interlocked.Increment(ref plugin.InFlightRuns);
            try
            {
                using var scope = plugin.Provider.CreateScope();
                var task = scope.ServiceProvider.GetServices<IPluginBackgroundTask>()
                    .FirstOrDefault(t => string.Equals(t.Name, taskName, StringComparison.OrdinalIgnoreCase));
                if (task is null)
                    throw new InvalidOperationException($"Plugin '{pluginName}' has no background task named '{taskName}'");

                await task.ExecuteAsync(scope.ServiceProvider, linkedCts.Token);
            }
            finally
            {
                Interlocked.Decrement(ref plugin.InFlightRuns);
            }
        }

        private async Task WaitForInFlightRunsAsync(LoadedPlugin plugin)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref plugin.InFlightRuns) > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(100);

            if (Volatile.Read(ref plugin.InFlightRuns) > 0)
                logger.LogWarning("Plugin {Name} still has {Count} in-flight background task run(s) after waiting 10 seconds; unloading anyway", plugin.Name, plugin.InFlightRuns);
        }

        private void RefreshEndpoints() =>
            endpointSource.Replace(_loaded.Values.SelectMany(p => p.Endpoints));

        private string PluginConnectionString(string pluginName)
        {
            var main = configuration.GetConnectionString("SchulyDatabase")
                ?? throw new InvalidOperationException("SchulyDatabase connection string not configured");
            var dbName = $"schuly_plugin_{pluginName.ToLowerInvariant().Replace(" ", "_")}";
            return new NpgsqlConnectionStringBuilder(main) { Database = dbName }.ConnectionString;
        }

        private IConfiguration LoadPluginConfig(ISchulyPlugin plugin)
        {
            var configDir = configuration["Plugins:ConfigDirectory"] ?? "plugins-config";
            if (!Path.IsPathRooted(configDir))
                configDir = Path.Combine(AppContext.BaseDirectory, configDir);

            var assemblyName = plugin.GetType().Assembly.GetName().Name ?? plugin.Name;
            var builder = new ConfigurationBuilder();
            foreach (var ext in new[] { "yml", "yaml" })
            {
                var path = Path.Combine(configDir, $"{assemblyName}.{ext}");
                if (File.Exists(path))
                {
                    builder.AddYamlFile(path, optional: true, reloadOnChange: true);
                    break;
                }
            }
            builder.AddEnvironmentVariables($"SCHULY_PLUGIN_{plugin.Name.ToUpperInvariant().Replace(" ", "_")}_");
            return builder.Build();
        }

        private sealed class HostServiceScope(IServiceProvider root) : IDisposable
        {
            private readonly IServiceScope _scope = root.CreateScope();
            public IServiceProvider Services => _scope.ServiceProvider;
            public void Dispose() => _scope.Dispose();
        }

        /// <summary>
        /// Bridges plugin code to the host's <see cref="IPluginUserContext"/> lazily.
        /// Constructing it touches no HttpContext, so plugin services that depend on the
        /// user context can be built outside a request (e.g. reading a plugin's catalog
        /// descriptor at load time); each call resolves the host implementation.
        /// <para>
        /// It resolves from a fresh <b>root</b> scope, never from
        /// <c>HttpContext.RequestServices</c>: for a plugin endpoint the request services
        /// are swapped to the plugin's own provider (see <see cref="PluginScopeMiddleware"/>),
        /// where this very type is registered as <see cref="IPluginUserContext"/> — resolving
        /// from there would re-enter this wrapper and recurse forever (stack overflow). The
        /// host's user service reads the caller's identity from the ambient
        /// <see cref="IHttpContextAccessor"/>, so a root scope still sees the real request user.
        /// </para>
        /// </summary>
        private sealed class DeferredPluginUserContext(IServiceProvider root) : IPluginUserContext
        {
            public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
            {
                using var scope = root.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IPluginUserContext>()
                    .GetCurrentUserIdAsync(cancellationToken);
            }

            public async Task<Guid?> GetCurrentSchoolUserIdAsync(CancellationToken cancellationToken = default)
            {
                using var scope = root.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IPluginUserContext>()
                    .GetCurrentSchoolUserIdAsync(cancellationToken);
            }
        }

        private sealed class LoadedPlugin
        {
            public required string Name { get; init; }
            public required string Version { get; init; }
            public required ISchulyPlugin Instance { get; init; }
            public required PluginLoadContext LoadContext { get; init; }
            public required Assembly Assembly { get; init; }
            public required ServiceProvider Provider { get; init; }
            public required IConfiguration Configuration { get; init; }
            public required AssemblyPart Part { get; init; }
            public required IReadOnlyList<Endpoint> Endpoints { get; init; }
            public required CancellationTokenSource TaskCts { get; init; }
            public int InFlightRuns;
        }
    }
}
