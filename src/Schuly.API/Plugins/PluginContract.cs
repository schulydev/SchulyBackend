using System.Reflection;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Plugins
{
    /// <summary>Decides whether a plugin assembly was built against a contract this backend provides.</summary>
    public static class PluginContract
    {
        public const string AssemblyName = "Schuly.Plugin.Abstractions";

        public static Version HostVersion { get; } = typeof(ISchulyPlugin).Assembly.GetName().Version!;

        private static readonly string[] BackendAssemblies = ["Schuly.Domain", "Schuly.Infrastructure", "Schuly.Application", "Schuly.API"];

        /// <summary>Same major; before 1.0 also the same minor; from 1.0 on the plugin's minor may not be newer than the host's.</summary>
        public static bool IsCompatible(Version plugin, Version host) =>
            plugin.Major == host.Major && (host.Major == 0 ? plugin.Minor == host.Minor : plugin.Minor <= host.Minor);

        /// <summary>Why the plugin can't load, or null when it can.</summary>
        public static string? Incompatibility(IEnumerable<AssemblyName> references, Version host)
        {
            foreach (var reference in references)
            {
                if (BackendAssemblies.Contains(reference.Name, StringComparer.OrdinalIgnoreCase))
                    return $"it references the backend assembly {reference.Name}. Plugins may only use {AssemblyName}; install a version built for contract {host.Major}.{host.Minor}.";

                if (string.Equals(reference.Name, AssemblyName, StringComparison.OrdinalIgnoreCase) && reference.Version is { } version && !IsCompatible(version, host))
                    return $"it was built for plugin contract {version.Major}.{version.Minor}, but this backend provides {host.Major}.{host.Minor}. Install a version built for {host.Major}.{host.Minor}.";
            }
            return null;
        }
    }
}
