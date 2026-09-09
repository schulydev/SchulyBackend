using Schuly.API.Plugins;

namespace Schuly.Tests.TestHelpers
{
    // Records what PluginHost asked TickerQ to do, for the hot-swap tests to assert on,
    // without needing a real database.
    public sealed class FakePluginTaskScheduler : IPluginTaskScheduler
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, IReadOnlyList<PluginTaskRegistration>> _synced = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _removed = [];

        public IReadOnlyDictionary<string, IReadOnlyList<PluginTaskRegistration>> Synced
        {
            get { lock (_gate) return new Dictionary<string, IReadOnlyList<PluginTaskRegistration>>(_synced, StringComparer.OrdinalIgnoreCase); }
        }

        public IReadOnlyList<string> Removed
        {
            get { lock (_gate) return _removed.ToList(); }
        }

        public Task SyncAsync(string plugin, IReadOnlyList<PluginTaskRegistration> tasks, CancellationToken cancellationToken = default)
        {
            lock (_gate) _synced[plugin] = tasks;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string plugin, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _removed.Add(plugin);
                _synced.Remove(plugin);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PluginTaskStatus>> SnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PluginTaskStatus>>([]);
    }
}
