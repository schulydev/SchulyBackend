using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Models;
using Schuly.Plugin.Abstractions;

namespace Schuly.Application.Queries.Plugins
{
    [AllowAuthenticated]
    public record GetPluginsQuery() : IQuery<Result<List<PluginDto>>>;

    public class GetPluginsQueryHandler(IReadOnlyList<ISchulyPlugin> plugins) : IQueryHandler<GetPluginsQuery, Result<List<PluginDto>>>
    {
        public ValueTask<Result<List<PluginDto>>> Handle(GetPluginsQuery query, CancellationToken cancellationToken)
        {
            var dtos = plugins.Select(p => new PluginDto(p.Name, p.Version)).ToList();
            return ValueTask.FromResult(Result<List<PluginDto>>.Success(dtos));
        }
    }
}
