using TickerQ.Utilities.Base;

namespace Schuly.API.Plugins
{
    public sealed record PluginTaskRequest(string Plugin, string Task);

    // The one host-side ticker function TickerQ knows about. Plugins are loaded at
    // runtime into collectible AssemblyLoadContexts, so a [TickerFunction] declared
    // inside a plugin assembly would never be seen by TickerQ's source generator,
    // which only runs at host compile time. Every plugin task is instead scheduled
    // as a ticker that carries the plugin/task name and dispatches through here.
    public sealed class PluginTaskRunner(PluginHost host)
    {
        [TickerFunction("RunPluginTask")]
        public Task RunAsync(TickerFunctionContext<PluginTaskRequest> context, CancellationToken cancellationToken) => host.RunTaskAsync(context.Request.Plugin, context.Request.Task, cancellationToken);
    }
}
