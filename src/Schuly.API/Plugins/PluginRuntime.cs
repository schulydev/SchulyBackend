using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// Endpoint metadata marking an endpoint (minimal-API or controller action) as
    /// belonging to a plugin. <see cref="PluginScopeMiddleware"/> reads it to run the
    /// request inside that plugin's child service scope.
    /// </summary>
    public sealed record PluginOwner(string PluginName);

    /// <summary>
    /// Maps plugin assemblies to plugin names. Updated by <see cref="PluginHost"/> on
    /// load/unload and read by the MVC convention so a dynamically-added controller's
    /// action descriptors are tagged with the owning plugin.
    /// </summary>
    public sealed class PluginAssemblyMap
    {
        private readonly ConcurrentDictionary<Assembly, string> _map = new();

        public void Add(Assembly assembly, string pluginName) => _map[assembly] = pluginName;
        public void Remove(Assembly assembly) => _map.TryRemove(assembly, out _);
        public string? Lookup(Assembly assembly) => _map.TryGetValue(assembly, out var name) ? name : null;
    }

    /// <summary>
    /// A service provider that resolves from a plugin's child provider first, then
    /// falls back to the request's root provider for shared framework/host services.
    /// Installed as <c>HttpContext.RequestServices</c> for plugin requests so plugin
    /// controllers and minimal-API delegates resolve their own services while still
    /// seeing the host's.
    /// </summary>
    internal sealed class FallbackServiceProvider(IServiceProvider primary, IServiceProvider fallback)
        : IServiceProvider, IKeyedServiceProvider, ISupportRequiredService
    {
        public object? GetService(Type serviceType) =>
            primary.GetService(serviceType) ?? fallback.GetService(serviceType);

        public object GetRequiredService(Type serviceType) =>
            primary.GetService(serviceType) ?? fallback.GetRequiredService(serviceType);

        public object? GetKeyedService(Type serviceType, object? serviceKey) =>
            (primary as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey)
            ?? (fallback as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey);

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
            (primary as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey)
            ?? ((IKeyedServiceProvider)fallback).GetRequiredKeyedService(serviceType, serviceKey);
    }
}
