using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Schuly.Infrastructure.Vault
{
    public interface IPluginVaultFactory
    {
        IPluginVault GetVault(string @namespace);
    }

    public sealed class PluginVaultFactory(VaultKeyring keyring) : IPluginVaultFactory
    {
        private readonly ConcurrentDictionary<string, IPluginVault> _vaults = new(StringComparer.Ordinal);

        public IPluginVault GetVault(string @namespace)
        {
            ArgumentException.ThrowIfNullOrEmpty(@namespace);
            return _vaults.GetOrAdd(@namespace, ns => new InMemoryVault(keyring.DeriveKey(ns)));
        }
    }

    public static class VaultServiceCollectionExtensions
    {
        public const string HostNamespace = "host";

        public static IServiceCollection AddSchulyVault(this IServiceCollection services)
        {
            services.AddSingleton<VaultKeyring>();
            services.AddSingleton<IPluginVaultFactory, PluginVaultFactory>();
            services.AddSingleton<IPluginVault>(sp =>
                sp.GetRequiredService<IPluginVaultFactory>().GetVault(HostNamespace));
            return services;
        }
    }
}
