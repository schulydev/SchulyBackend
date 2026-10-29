namespace Schuly.API.Plugins
{
    public sealed class PluginManager(PluginHost host, PluginStore store, PluginSet set, PluginRegistryClient registry, ILogger<PluginManager> logger)
    {
        private static string HostContract => $"{PluginContract.HostVersion.Major}.{PluginContract.HostVersion.Minor}";

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
                    if (entry is not null && !entry.SupportsContract(PluginContract.HostVersion))
                    {
                        logger.LogWarning("Plugin {Name} v{Version} needs plugin contract {Contract}, but this backend provides {Host}; keeping the installed version", d.Name, entry.Version, entry.Contract, HostContract);
                        if (installed is null) continue;
                        entry = null;
                    }
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

        private static RegistryPlugin? Resolve(IReadOnlyList<RegistryPlugin> index, string name, string? version)
        {
            var entry = index.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return entry?.WithPinnedVersion(version);
        }
    }
}
