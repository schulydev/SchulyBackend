using System.Collections.Concurrent;

namespace Schuly.API.Services
{
    /// <summary>
    /// One background task's run health. Captures the task-level outcome (the
    /// whole sync loop); per-account detail lives on each plugin's own sync
    /// endpoint.
    /// </summary>
    // LastStatus is one of: Pending | Running | Success | Failed
    public record PluginTaskStatus(string Name, double IntervalSeconds, string LastStatus, DateTime? LastStartedAt, DateTime? LastFinishedAt, long? LastDurationMs, string? LastError, DateTime? NextRunAt, int TotalRuns, int TotalFailures, int ConsecutiveFailures);

    /// <summary>
    /// In-memory registry of plugin background-task runs, updated by the task loops
    /// the <see cref="Schuly.API.Plugins.PluginHost"/> runs per loaded plugin. Resets on
    /// restart — it reflects live scheduler health, not durable history.
    /// </summary>
    public class PluginSchedulerRegistry
    {
        private sealed class Entry
        {
            public double IntervalSeconds;
            public string LastStatus = "Pending";
            public DateTime? LastStartedAt;
            public DateTime? LastFinishedAt;
            public long? LastDurationMs;
            public string? LastError;
            public int TotalRuns;
            public int TotalFailures;
            public int ConsecutiveFailures;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new();

        public void Register(string name, TimeSpan interval) =>
            _entries.AddOrUpdate(name,
                _ => new Entry { IntervalSeconds = interval.TotalSeconds },
                (_, e) => { e.IntervalSeconds = interval.TotalSeconds; return e; });

        public void RecordStart(string name)
        {
            var e = _entries.GetOrAdd(name, _ => new Entry());
            e.LastStartedAt = DateTime.UtcNow;
            e.LastStatus = "Running";
        }

        public void RecordSuccess(string name, long durationMs)
        {
            var e = _entries.GetOrAdd(name, _ => new Entry());
            e.LastFinishedAt = DateTime.UtcNow;
            e.LastDurationMs = durationMs;
            e.LastStatus = "Success";
            e.LastError = null;
            e.TotalRuns++;
            e.ConsecutiveFailures = 0;
        }

        public void RecordFailure(string name, long durationMs, string error)
        {
            var e = _entries.GetOrAdd(name, _ => new Entry());
            e.LastFinishedAt = DateTime.UtcNow;
            e.LastDurationMs = durationMs;
            e.LastStatus = "Failed";
            e.LastError = error;
            e.TotalRuns++;
            e.TotalFailures++;
            e.ConsecutiveFailures++;
        }

        public IReadOnlyList<PluginTaskStatus> Snapshot() =>
            _entries.Select(kv => new PluginTaskStatus(
                Name: kv.Key,
                IntervalSeconds: kv.Value.IntervalSeconds,
                LastStatus: kv.Value.LastStatus,
                LastStartedAt: kv.Value.LastStartedAt,
                LastFinishedAt: kv.Value.LastFinishedAt,
                LastDurationMs: kv.Value.LastDurationMs,
                LastError: kv.Value.LastError,
                NextRunAt: kv.Value.LastFinishedAt is { } f && kv.Value.IntervalSeconds > 0
                    ? f.AddSeconds(kv.Value.IntervalSeconds) : null,
                TotalRuns: kv.Value.TotalRuns,
                TotalFailures: kv.Value.TotalFailures,
                ConsecutiveFailures: kv.Value.ConsecutiveFailures))
            .OrderBy(s => s.Name)
            .ToList();
    }
}
