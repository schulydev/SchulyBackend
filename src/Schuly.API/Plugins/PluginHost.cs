using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Schuly.API.Services;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Storage;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Plugins
{
    /// <summary>Lightweight view of a loaded plugin for the API.</summary>
    public sealed record LoadedPluginInfo(string Name, string Version);

    /// <summary>
    /// Loads and unloads plugins at runtime. Each plugin runs in its own collectible
    /// <see cref="PluginLoadContext"/> and its own child <see cref="IServiceProvider"/>;
    /// its controllers (MVC ApplicationParts), minimal-API endpoints, and background
    /// tasks are wired in on load and torn down on unload — no process restart.
    /// </summary>
    public sealed class PluginHost(
        IServiceProvider rootProvider,
        IConfiguration configuration,
        ApplicationPartManager partManager,
        PluginEndpointDataSource endpointSource,
        PluginAssemblyMap assemblyMap,
        PluginSchedulerRegistry scheduler,
        ILogger<PluginHost> logger)
    {
        private readonly ConcurrentDictionary<string, LoadedPlugin> _loaded = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _gate = new(1, 1);

        public IServiceProvider? GetProvider(string pluginName) =>
            _loaded.TryGetValue(pluginName, out var p) ? p.Provider : null;

        public IReadOnlyList<LoadedPluginInfo> List() =>
            _loaded.Values.Select(p => new LoadedPluginInfo(p.Name, p.Version)).ToList();

        /// <summary>Live snapshot of the loaded plugin instances (for read-only queries).</summary>
        public IReadOnlyList<ISchulyPlugin> Instances() =>
            _loaded.Values.Select(p => p.Instance).ToList();

        public bool IsLoaded(string name) => _loaded.ContainsKey(name);

        /// <summary>
        /// Resolves the <see cref="IPluginLogin"/> across loaded plugins whose
        /// <see cref="IPluginLogin.SystemKey"/> matches <paramref name="systemKey"/> and
        /// runs its connect inside that plugin's own DI scope — so the login's scoped
        /// services (DbContext, vault, the request's user context) resolve correctly.
        /// Returns null when no loaded plugin handles the system. The host controller
        /// can't inject these directly: plugin logins live in each plugin's child
        /// provider, not the frozen root container, so this bridge is the only path.
        /// </summary>
        public async Task<PluginLoginResult?> ConnectAsync(
            string systemKey,
            IReadOnlyDictionary<string, string> fields,
            string? displayName,
            CancellationToken ct = default)
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

        /// <summary>
        /// Seeds the school-systems catalog from a freshly-loaded plugin's
        /// <see cref="IPluginLogin.SchoolSystem"/> descriptors (seed-if-missing by
        /// <see cref="SchoolSystem.Key"/>), so the catalog is plugin-provided instead of
        /// operator config. Anything an admin edits afterwards is left untouched.
        /// </summary>
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

                var provider = BuildChildProvider(plugin);
                await plugin.MigrateAsync(provider, ct);

                // Minimal-API endpoints (plugin.ConfigureEndpoints).
                var endpoints = PluginEndpointDataSource.Build(plugin.Name, rootProvider, plugin.ConfigureEndpoints);

                // MVC controllers shipped in the plugin assembly.
                var part = new AssemblyPart(assembly);
                partManager.ApplicationParts.Add(part);
                assemblyMap.Add(assembly, plugin.Name);

                var loaded = new LoadedPlugin
                {
                    Name = plugin.Name,
                    Version = plugin.Version,
                    Instance = plugin,
                    LoadContext = alc,
                    Assembly = assembly,
                    Provider = provider,
                    Part = part,
                    Endpoints = endpoints,
                };
                _loaded[plugin.Name] = loaded;

                RefreshEndpoints();
                PluginActionDescriptorChangeProvider.Instance.NotifyChanged();
                StartBackgroundTasks(loaded);
                await SyncSchoolSystemsAsync(loaded, ct);

                logger.LogInformation("Loaded plugin {Name} v{Version}", plugin.Name, plugin.Version);
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

                plugin.TaskCts?.Cancel();

                partManager.ApplicationParts.Remove(plugin.Part);
                assemblyMap.Remove(plugin.Assembly);
                PluginActionDescriptorChangeProvider.Instance.NotifyChanged();
                RefreshEndpoints();

                if (plugin.Instance is IAsyncDisposable instanceAsync)
                    await instanceAsync.DisposeAsync();
                else if (plugin.Instance is IDisposable instanceSync)
                    instanceSync.Dispose();

                await plugin.Provider.DisposeAsync();

                plugin.TaskCts?.Dispose();
                plugin.LoadContext.Unload();

                // Best-effort: help the collectible context actually unload.
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

        private ServiceProvider BuildChildProvider(ISchulyPlugin plugin)
        {
            var services = new ServiceCollection();

            // Forward the shared host services a plugin may depend on. Plugin-specific
            // services come from the plugin's own ConfigureServices below.
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

            // The plugin's own isolated vault, keyed by its name (matches how plugins
            // resolve it: [FromKeyedServices(PluginName)] IPluginVault).
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

            return services.BuildServiceProvider();
        }

        private void StartBackgroundTasks(LoadedPlugin plugin)
        {
            var tasks = plugin.Provider.GetServices<IPluginBackgroundTask>().ToList();
            if (tasks.Count == 0)
                return;

            plugin.TaskCts = new CancellationTokenSource();
            foreach (var task in tasks)
                _ = RunTaskLoop(task, plugin.Provider, plugin.TaskCts.Token);
        }

        private async Task RunTaskLoop(IPluginBackgroundTask task, IServiceProvider provider, CancellationToken ct)
        {
            scheduler.Register(task.Name, task.Interval);
            while (!ct.IsCancellationRequested)
            {
                var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                scheduler.RecordStart(task.Name);
                try
                {
                    await task.ExecuteAsync(provider, ct);
                    scheduler.RecordSuccess(task.Name, ElapsedMs(startedAt));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    scheduler.RecordFailure(task.Name, ElapsedMs(startedAt), ex.Message);
                    logger.LogError(ex, "Plugin background task '{Name}' failed", task.Name);
                }

                try { await Task.Delay(task.Interval, ct); }
                catch (OperationCanceledException) { break; }
            }
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

        private static long ElapsedMs(long start) =>
            (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        /// <summary>
        /// A host DI scope owned by a plugin scope, so plugin-scoped services can pull
        /// scoped host services (the main DB, blob storage) from it. Disposed when the
        /// plugin scope is disposed.
        /// </summary>
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
            public required AssemblyPart Part { get; init; }
            public required IReadOnlyList<Endpoint> Endpoints { get; init; }
            public CancellationTokenSource? TaskCts { get; set; }
        }
    }
}
