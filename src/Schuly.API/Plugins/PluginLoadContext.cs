using System.Reflection;
using System.Runtime.Loader;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// A collectible <see cref="AssemblyLoadContext"/> for a single plugin, so the
    /// plugin (and its private dependencies) can be loaded and later unloaded at
    /// runtime — the basis for hot-swapping without restarting the process.
    ///
    /// Plugin-private dependency DLLs live next to the plugin in the plugins
    /// directory (extracted from the plugin's <c>-deps.zip</c>). Shared contracts and
    /// framework assemblies (<c>Schuly.Plugin.Abstractions</c>, ASP.NET, BCL, Mediator)
    /// are deliberately resolved from the <see cref="AssemblyLoadContext.Default"/>
    /// context so type identity is preserved across the host/plugin boundary — an
    /// <c>ISchulyPlugin</c> loaded here must be the same type the host knows.
    /// </summary>
    public sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string _pluginDirectory;

        public PluginLoadContext(string name, string pluginDirectory)
            : base(name, isCollectible: true)
        {
            _pluginDirectory = pluginDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (name is null)
                return null;

            // Shared contracts + anything already loaded by the host must come from the
            // default context, or the plugin would see a *different* Type and casts to
            // ISchulyPlugin / framework abstractions would fail.
            if (IsSharedWithHost(name))
                return null;

            // Otherwise resolve plugin-private deps from the plugin folder.
            var candidate = Path.Combine(_pluginDirectory, name + ".dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }

        private bool IsSharedWithHost(string simpleName)
        {
            // The plugin contract is always the host's type.
            if (simpleName.StartsWith("Schuly.Plugin.Abstractions", StringComparison.OrdinalIgnoreCase))
                return true;

            // Anything the host has already loaded is shared, so a plugin referencing a
            // library the backend also uses sees the one Type instance (Schuly.Domain,
            // Mediator, the shared framework, …).
            foreach (var loaded in Default.Assemblies)
            {
                if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // A dependency the plugin ships next to itself (e.g. Microsoft.Kiota.*,
            // AngleSharp, System.ClientModel) that the host doesn't use is plugin-private
            // — load it from the plugin folder even though its name is Microsoft.*/System.*.
            if (File.Exists(Path.Combine(_pluginDirectory, simpleName + ".dll")))
                return false;

            // Core framework assemblies the host hasn't touched yet aren't shipped with
            // the plugin — let the default context resolve them from the shared framework.
            return simpleName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   simpleName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                   simpleName.StartsWith("Mediator", StringComparison.OrdinalIgnoreCase) ||
                   simpleName is "netstandard" or "mscorlib";
        }
    }
}
