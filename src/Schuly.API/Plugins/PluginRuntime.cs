using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Schuly.API.Plugins
{
    public sealed record PluginOwner(string PluginName);

    public sealed class PluginAssemblyMap
    {
        private readonly ConcurrentDictionary<Assembly, string> _map = new();

        public void Add(Assembly assembly, string pluginName) => _map[assembly] = pluginName;
        public void Remove(Assembly assembly) => _map.TryRemove(assembly, out _);
        public string? Lookup(Assembly assembly) => _map.TryGetValue(assembly, out var name) ? name : null;
    }

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
