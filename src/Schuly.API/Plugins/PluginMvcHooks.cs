using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// Lets the host force MVC to rebuild its action descriptors after a plugin's
    /// controllers are added to / removed from the <c>ApplicationPartManager</c> at
    /// runtime. Without this, dynamically-added controllers are never routed.
    /// </summary>
    public sealed class PluginActionDescriptorChangeProvider : IActionDescriptorChangeProvider
    {
        public static readonly PluginActionDescriptorChangeProvider Instance = new();

        private CancellationTokenSource _cts = new();

        public IChangeToken GetChangeToken() => new CancellationChangeToken(_cts.Token);

        /// <summary>Signals MVC to recompute the action descriptor collection.</summary>
        public void NotifyChanged()
        {
            var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            old.Cancel();
            old.Dispose();
        }
    }

    /// <summary>
    /// Tags every controller action whose declaring assembly belongs to a loaded
    /// plugin with <see cref="PluginOwner"/> endpoint metadata, so the scope middleware
    /// can run the request inside that plugin's child service scope. Runs on every
    /// action-descriptor rebuild, so it re-applies as plugins come and go.
    /// </summary>
    public sealed class PluginControllerConvention(PluginAssemblyMap map) : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                var assembly = controller.ControllerType.Assembly;
                var pluginName = map.Lookup(assembly);
                if (pluginName is null)
                    continue;

                var owner = new PluginOwner(pluginName);
                foreach (var selector in controller.Selectors)
                    selector.EndpointMetadata.Add(owner);

                foreach (var action in controller.Actions)
                    foreach (var selector in action.Selectors)
                        selector.EndpointMetadata.Add(owner);
            }
        }
    }
}
