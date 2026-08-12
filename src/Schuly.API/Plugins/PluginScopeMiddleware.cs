using Microsoft.Extensions.DependencyInjection;

namespace Schuly.API.Plugins
{
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
