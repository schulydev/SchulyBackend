namespace Schuly.API.Plugins
{
    /// <summary>
    /// Orchestrates the plugin lifecycle: reconciles the declarative <c>plugins.yml</c>
    /// against the registry + disk on startup, and serves the admin install / update /
    /// remove operations. Every change is applied in-process via <see cref="PluginHost"/>
    /// (no restart) and persisted to <c>plugins.yml</c> so it survives one.
    /// </summary>
    public sealed class PluginManager(
        PluginHost host,
        PluginStore store,
        PluginSet set,
        PluginRegistryClient registry,
        ILogger<PluginManager> logger)
    {
        public Task<IReadOnlyList<RegistryPlugin>> GetRegistryAsync(CancellationToken ct = default) =>
            registry.FetchIndexAsync(ct);

        public IReadOnlyList<LoadedPluginInfo> Loaded() => host.List();

        /// <summary>
        /// Brings disk + the running process in line with <c>plugins.yml</c>: downloads
        /// missing/outdated plugins, removes ones no longer desired, then loads the set.
        /// Called once at startup.
        /// </summary>
        public async Task ReconcileAsync(CancellationToken ct = default)
        {
            var desired = set.Read();
            IReadOnlyList<RegistryPlugin> index = [];
            if (desired.Count > 0)
            {
                try { index = await registry.FetchIndexAsync(ct); }
                catch (Exception ex) { logger.LogWarning(ex, "Plugin registry unreachable; using installed plugins as-is"); }
            }

            // Remove anything installed but no longer desired.
            foreach (var installed in store.List())
            {
                if (!desired.Any(d => d.Name.Equals(installed.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (host.IsLoaded(installed.Name)) await host.UnloadAsync(installed.Name, ct);
                    store.Remove(installed.Name);
                    logger.LogInformation("Removed undeclared plugin {Name}", installed.Name);
                }
            }

            // Ensure each desired plugin is present at the right version.
            foreach (var d in desired)
            {
                try
                {
                    var entry = Resolve(index, d.Name, d.Version);
                    var installed = store.Find(d.Name);
                    if (entry is not null && (installed is null || !installed.Version.Equals(entry.Version, StringComparison.OrdinalIgnoreCase)))
                    {
                        await store.InstallAsync(entry, registry, ct);
                    }
                    else if (entry is null && installed is null)
                    {
                        logger.LogWarning("Plugin {Name} not found in registry and not installed; skipping", d.Name);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to provision plugin {Name}; leaving existing install untouched", d.Name);
                }
            }

            // Load everything now on disk that isn't already loaded.
            foreach (var manifest in store.List())
            {
                if (!host.IsLoaded(manifest.Name))
                {
                    try { await host.LoadAsync(manifest, store.Directory, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Failed to load plugin {Name}", manifest.Name); }
                }
            }
        }

        public async Task InstallAsync(string name, string? version, CancellationToken ct = default)
        {
            var entry = await registry.ResolveAsync(name, version, ct)
                ?? throw new InvalidOperationException($"Plugin '{name}' not found in the registry");

            if (host.IsLoaded(name)) await host.UnloadAsync(name, ct);
            var manifest = await store.InstallAsync(entry, registry, ct);
            set.Upsert(name, string.IsNullOrWhiteSpace(version) ? "latest" : version);
            await host.LoadAsync(manifest, store.Directory, ct);
        }

        public Task UpdateAsync(string name, CancellationToken ct = default) => InstallAsync(name, "latest", ct);

        public async Task RemoveAsync(string name, CancellationToken ct = default)
        {
            if (host.IsLoaded(name)) await host.UnloadAsync(name, ct);
            store.Remove(name);
            set.RemoveEntry(name);
        }

        private static RegistryPlugin? Resolve(IReadOnlyList<RegistryPlugin> index, string name, string? version)
        {
            var entry = index.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return null;

            if (!string.IsNullOrWhiteSpace(version) &&
                !version.Equals("latest", StringComparison.OrdinalIgnoreCase) &&
                !version.Equals(entry.Version, StringComparison.OrdinalIgnoreCase))
            {
                return entry with
                {
                    Version = version,
                    Dll = $"{entry.Name}-v{version}.dll",
                    Deps = $"{entry.Name}-v{version}-deps.zip",
                };
            }
            return entry;
        }
    }
}
