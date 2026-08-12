namespace Schuly.API.Plugins
{
    public sealed class PluginManager(PluginHost host, PluginStore store, PluginSet set, PluginRegistryClient registry, ILogger<PluginManager> logger)
    {
        public Task<IReadOnlyList<RegistryPlugin>> GetRegistryAsync(CancellationToken ct = default) =>
            registry.FetchIndexAsync(ct);

        public IReadOnlyList<LoadedPluginInfo> Loaded() => host.List();

        public async Task ReconcileAsync(CancellationToken ct = default)
        {
            var desired = set.Read();
            IReadOnlyList<RegistryPlugin> index = [];
            if (desired.Count > 0)
            {
                try { index = await registry.FetchIndexAsync(ct); }
                catch (Exception ex) { logger.LogWarning(ex, "Plugin registry unreachable; using installed plugins as-is"); }
            }

            foreach (var installed in store.List())
            {
                if (!desired.Any(d => d.Name.Equals(installed.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (host.IsLoaded(installed.Name)) await host.UnloadAsync(installed.Name, ct);
                    store.Remove(installed.Name);
                    logger.LogInformation("Removed undeclared plugin {Name}", installed.Name);
                }
            }

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
            return entry?.WithPinnedVersion(version);
        }
    }
}
