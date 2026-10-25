using Schuly.Application.Abstractions;
using Schuly.Infrastructure.Services;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Plugins
{
    /// <summary>Wipes a deleted user's plugin-owned data by dispatching <see cref="PluginUserPurgeEvent"/> to every plugin.</summary>
    public sealed class PluginUserDataPurger(IPluginEventDispatcher dispatcher) : IPluginUserDataPurger
    {
        public Task PurgeAsync(Guid applicationUserId, CancellationToken cancellationToken = default) =>
            dispatcher.DispatchAsync(new PluginUserPurgeEvent(applicationUserId), cancellationToken);
    }
}
