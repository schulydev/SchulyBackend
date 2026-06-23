using Microsoft.Extensions.DependencyInjection;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// For a request whose matched endpoint belongs to a plugin (tagged with
    /// <see cref="PluginOwner"/>), swaps <c>HttpContext.RequestServices</c> to a scope
    /// from that plugin's child provider (falling back to the host's services). This is
    /// what lets a hot-loaded plugin's controllers and minimal-API delegates resolve
    /// their own DI registrations — which live in the plugin's container, not the
    /// frozen root one. Must run after routing + authorization and before the endpoint.
    /// </summary>
    public sealed class PluginScopeMiddleware(RequestDelegate next, PluginHost host)
    {
        public async Task Invoke(HttpContext context)
        {
            var owner = context.GetEndpoint()?.Metadata.GetMetadata<PluginOwner>();
            var provider = owner is null ? null : host.GetProvider(owner.PluginName);
            if (provider is null)
            {
                await next(context);
                return;
            }

            var original = context.RequestServices;
            using var scope = provider.CreateScope();
            context.RequestServices = new FallbackServiceProvider(scope.ServiceProvider, original);
            try
            {
                await next(context);
            }
            finally
            {
                context.RequestServices = original;
            }
        }
    }
}
