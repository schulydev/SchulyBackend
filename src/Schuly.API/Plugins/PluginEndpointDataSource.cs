using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace Schuly.API.Plugins
{
    /// <summary>
    /// A mutable <see cref="EndpointDataSource"/> holding the minimal-API endpoints
    /// every loaded plugin contributed via <c>ConfigureEndpoints</c>. Replacing the set
    /// raises a change token so the router rebuilds — that's what makes plugin routes
    /// appear and disappear without a restart.
    /// </summary>
    public sealed class PluginEndpointDataSource : EndpointDataSource
    {
        private readonly object _gate = new();
        private List<Endpoint> _endpoints = [];
        private CancellationTokenSource _cts = new();
        private IChangeToken _changeToken;

        public PluginEndpointDataSource() => _changeToken = new CancellationChangeToken(_cts.Token);

        public override IReadOnlyList<Endpoint> Endpoints
        {
            get { lock (_gate) return _endpoints; }
        }

        public override IChangeToken GetChangeToken()
        {
            lock (_gate) return _changeToken;
        }

        public void Replace(IEnumerable<Endpoint> endpoints)
        {
            CancellationTokenSource old;
            lock (_gate)
            {
                _endpoints = endpoints.ToList();
                old = _cts;
                _cts = new CancellationTokenSource();
                _changeToken = new CancellationChangeToken(_cts.Token);
            }
            old.Cancel();
            old.Dispose();
        }

        public static IReadOnlyList<Endpoint> Build(string pluginName, IServiceProvider rootProvider, Action<IEndpointRouteBuilder> configure)
        {
            var builder = new CapturingEndpointRouteBuilder(rootProvider);
            configure(builder);

            var result = new List<Endpoint>();
            foreach (var dataSource in builder.DataSources)
            {
                foreach (var endpoint in dataSource.Endpoints)
                {
                    if (endpoint is RouteEndpoint route)
                    {
                        var metadata = new List<object>(route.Metadata) { new PluginOwner(pluginName) };
                        result.Add(new RouteEndpoint(
                            route.RequestDelegate!,
                            route.RoutePattern,
                            route.Order,
                            new EndpointMetadataCollection(metadata),
                            route.DisplayName));
                    }
                    else
                    {
                        result.Add(endpoint);
                    }
                }
            }
            return result;
        }

        private sealed class CapturingEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;
            public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();
            public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
        }
    }
}
