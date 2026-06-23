using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Schuly.Infrastructure.Vault
{
    /// <summary>Hands out per-namespace <see cref="IPluginVault"/> instances, one cached vault per namespace.</summary>
    public interface IPluginVaultFactory
    {
        /// <summary>
        /// Returns the vault for <paramref name="namespace"/>, creating it on first use. The
        /// same namespace always maps to the same isolated vault; different namespaces get
        /// independently-keyed vaults that cannot read one another's values.
        /// </summary>
        IPluginVault GetVault(string @namespace);
    }

    /// <inheritdoc />
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
        /// <summary>Namespace of the vault the backend itself may use (resolve <see cref="IPluginVault"/> directly).</summary>
        public const string HostNamespace = "host";

        /// <summary>
        /// Registers the vault keyring (generates the startup master secret), the per-namespace
        /// factory, and a default host vault. Plugins get their own isolated vaults wired up by
        /// the plugin host; the backend can inject <see cref="IPluginVault"/> for the host vault,
        /// but isn't required to use it.
        /// </summary>
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
