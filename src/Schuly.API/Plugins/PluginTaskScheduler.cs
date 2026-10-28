using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Plugins
{
    public sealed record PluginTaskRegistration(string Plugin, string Task, string Cron, int Retries, IReadOnlyList<TimeSpan>? RetryIntervals, bool RunOnStartup);

    public sealed record PluginTaskStatus(string Plugin, string Name, string Cron, double IntervalSeconds, string LastStatus, DateTime? LastStartedAt, DateTime? LastFinishedAt, long? LastDurationMs, string? LastError, DateTime? NextRunAt, int TotalRuns, int TotalFailures, int ConsecutiveFailures);

    public interface IPluginTaskScheduler
    {
        Task SyncAsync(string plugin, IReadOnlyList<PluginTaskRegistration> tasks, CancellationToken cancellationToken = default);
        Task RemoveAsync(string plugin, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PluginTaskStatus>> SnapshotAsync(CancellationToken cancellationToken = default);
    }

    public interface IPluginTaskRunner
    {
        Task RunTaskAsync(string pluginName, string taskName, CancellationToken cancellationToken = default);
    }

    // Resolves a task's effective schedule: the plugin's own PluginSchedule, with any
    // per-task operator override from the plugin's merged configuration applied on top.
    public static class PluginTaskScheduleResolver
    {
        public static PluginTaskRegistration Resolve(string plugin, IPluginBackgroundTask task, IConfiguration pluginConfiguration, ILogger logger)
        {
            var schedule = task.Schedule;
            var section = pluginConfiguration.GetSection($"Schedules:{task.Name}");

            var cronOverride = section["Cron"];
            if (!string.IsNullOrWhiteSpace(cronOverride))
            {
                try { schedule = schedule with { Cron = cronOverride }; }
                catch (ArgumentException ex) { logger.LogWarning("Plugin {Plugin} task {Task}: ignoring invalid Cron override '{Cron}' ({Message})", plugin, task.Name, cronOverride, ex.Message); }
            }

            var retriesOverride = section["Retries"];
            if (!string.IsNullOrWhiteSpace(retriesOverride))
            {
                if (int.TryParse(retriesOverride, out var retries) && retries >= 0)
                {
                    try { schedule = schedule with { Retries = retries }; }
                    catch (ArgumentException ex) { logger.LogWarning("Plugin {Plugin} task {Task}: ignoring invalid Retries override '{Retries}' ({Message})", plugin, task.Name, retriesOverride, ex.Message); }
                }
                else
                {
                    logger.LogWarning("Plugin {Plugin} task {Task}: ignoring invalid Retries override '{Retries}'", plugin, task.Name, retriesOverride);
                }
            }

            var runOnStartupOverride = section["RunOnStartup"];
            if (!string.IsNullOrWhiteSpace(runOnStartupOverride))
            {
                if (bool.TryParse(runOnStartupOverride, out var runOnStartup))
                    schedule = schedule with { RunOnStartup = runOnStartup };
                else
                    logger.LogWarning("Plugin {Plugin} task {Task}: ignoring invalid RunOnStartup override '{RunOnStartup}'", plugin, task.Name, runOnStartupOverride);
            }

            return new PluginTaskRegistration(plugin, task.Name, schedule.Cron, schedule.Retries, schedule.RetryIntervals, schedule.RunOnStartup);
        }
    }

    // Keeps every registered plugin task in memory and fires it from a one-second tick. Each
    // task's schedule, last run and run counters are stored in the PluginTasks table.
    public sealed class PluginTaskScheduler(IServiceScopeFactory scopeFactory, IServiceProvider services, ILogger<PluginTaskScheduler> logger) : BackgroundService, IPluginTaskScheduler
    {
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(30);

        private sealed class ScheduledTask(PluginTaskRegistration registration, CrontabSchedule schedule, DateTime nextRunAt)
        {
            public PluginTaskRegistration Registration { get; } = registration;
            public CrontabSchedule Schedule { get; } = schedule;
            public DateTime NextRunAt { get; set; } = nextRunAt;
            public int Running;
        }

        private readonly ConcurrentDictionary<(string Plugin, string Task), ScheduledTask> _tasks = new();

        public static CrontabSchedule? ParseCron(string cron) => CrontabSchedule.TryParse(cron.Replace('?', '*'));

        public async Task SyncAsync(string plugin, IReadOnlyList<PluginTaskRegistration> tasks, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                var existing = await db.PluginTasks.Where(t => t.PluginName == plugin).ToListAsync(cancellationToken);
                var names = new HashSet<string>(StringComparer.Ordinal);

                foreach (var registration in tasks)
                {
                    var schedule = ParseCron(registration.Cron);
                    if (schedule is null)
                    {
                        logger.LogWarning("Plugin {Plugin} task {Task}: cron '{Cron}' can't be scheduled", plugin, registration.Task, registration.Cron);
                        continue;
                    }

                    names.Add(registration.Task);
                    var row = existing.FirstOrDefault(t => t.TaskName == registration.Task);
                    var missedRun = row is { NextRunAt: { } due } && due <= now && row.Cron == registration.Cron;
                    var nextRunAt = registration.RunOnStartup || missedRun ? now : schedule.GetNextOccurrence(now);
                    _tasks[(plugin, registration.Task)] = new ScheduledTask(registration, schedule, nextRunAt);

                    if (row is null)
                    {
                        row = new PluginTask { PluginName = plugin, TaskName = registration.Task, Cron = registration.Cron };
                        db.PluginTasks.Add(row);
                    }

                    row.Cron = registration.Cron;
                    row.NextRunAt = nextRunAt;
                    row.UpdatedAt = now;
                    if (row.LastStatus == "Running")
                        row.LastStatus = "Cancelled";
                }

                foreach (var key in _tasks.Keys.Where(k => k.Plugin == plugin && !names.Contains(k.Task)))
                    _tasks.TryRemove(key, out _);

                db.PluginTasks.RemoveRange(existing.Where(t => !names.Contains(t.TaskName)));
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync scheduled tasks for plugin {Plugin}", plugin);
            }
        }

        public async Task RemoveAsync(string plugin, CancellationToken cancellationToken = default)
        {
            foreach (var key in _tasks.Keys.Where(k => k.Plugin == plugin))
                _tasks.TryRemove(key, out _);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                db.PluginTasks.RemoveRange(await db.PluginTasks.Where(t => t.PluginName == plugin).ToListAsync(cancellationToken));
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove scheduled tasks for plugin {Plugin}", plugin);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Tick);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    _ = RunDueAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public Task RunDueAsync(DateTime now, CancellationToken cancellationToken = default)
        {
            var runs = new List<Task>();
            foreach (var (key, task) in _tasks)
            {
                if (task.NextRunAt > now || Interlocked.CompareExchange(ref task.Running, 1, 0) != 0)
                    continue;

                task.NextRunAt = task.Schedule.GetNextOccurrence(now);
                runs.Add(RunAsync(key, task, cancellationToken));
            }

            return Task.WhenAll(runs);
        }

        private async Task RunAsync((string Plugin, string Task) key, ScheduledTask task, CancellationToken cancellationToken)
        {
            try
            {
                var startedAt = DateTime.UtcNow;
                await UpdateRowAsync(key, row =>
                {
                    row.LastStatus = "Running";
                    row.LastStartedAt = startedAt;
                    row.NextRunAt = task.NextRunAt;
                });

                var (status, error) = await ExecuteWithRetriesAsync(key, task.Registration, cancellationToken);
                var finishedAt = DateTime.UtcNow;

                await UpdateRowAsync(key, row =>
                {
                    row.LastStatus = status;
                    row.LastFinishedAt = finishedAt;
                    row.LastDurationMs = (long)(finishedAt - startedAt).TotalMilliseconds;
                    row.LastError = error?[..Math.Min(error.Length, 2000)];
                    if (status == "Cancelled")
                        return;

                    row.TotalRuns++;
                    if (status == "Failed")
                    {
                        row.TotalFailures++;
                        row.ConsecutiveFailures++;
                    }
                    else
                    {
                        row.ConsecutiveFailures = 0;
                    }
                });
            }
            finally
            {
                Volatile.Write(ref task.Running, 0);
            }
        }

        private async Task<(string Status, string? Error)> ExecuteWithRetriesAsync((string Plugin, string Task) key, PluginTaskRegistration registration, CancellationToken cancellationToken)
        {
            var runner = services.GetRequiredService<IPluginTaskRunner>();
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await runner.RunTaskAsync(key.Plugin, key.Task, cancellationToken);
                    return ("Success", null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return ("Cancelled", null);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Plugin task {Plugin}/{Task} failed (attempt {Attempt} of {Attempts})", key.Plugin, key.Task, attempt + 1, registration.Retries + 1);
                    if (attempt >= registration.Retries)
                        return ("Failed", ex.Message);
                }

                try
                {
                    await Task.Delay(RetryDelay(registration, attempt), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return ("Cancelled", null);
                }
            }
        }

        private static TimeSpan RetryDelay(PluginTaskRegistration registration, int attempt) =>
            registration.RetryIntervals is { Count: > 0 } intervals ? intervals[Math.Min(attempt, intervals.Count - 1)] : DefaultRetryInterval;

        private async Task UpdateRowAsync((string Plugin, string Task) key, Action<PluginTask> update)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                var row = await db.PluginTasks.FirstOrDefaultAsync(t => t.PluginName == key.Plugin && t.TaskName == key.Task);
                if (row is null)
                    return;

                update(row);
                row.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to record the run of plugin task {Plugin}/{Task}", key.Plugin, key.Task);
            }
        }

        public async Task<IReadOnlyList<PluginTaskStatus>> SnapshotAsync(CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            var rows = await db.PluginTasks.AsNoTracking().OrderBy(t => t.PluginName).ThenBy(t => t.TaskName).ToListAsync(cancellationToken);

            return rows.Select(BuildStatus).ToList();
        }

        private static PluginTaskStatus BuildStatus(PluginTask row)
        {
            var intervalSeconds = 0d;
            if (ParseCron(row.Cron) is { } schedule)
            {
                var next1 = schedule.GetNextOccurrence(DateTime.UtcNow);
                intervalSeconds = (schedule.GetNextOccurrence(next1) - next1).TotalSeconds;
            }

            return new PluginTaskStatus(row.PluginName, row.TaskName, row.Cron, intervalSeconds, row.LastStatus, row.LastStartedAt, row.LastFinishedAt, row.LastDurationMs, row.LastError, row.NextRunAt, row.TotalRuns, row.TotalFailures, row.ConsecutiveFailures);
        }
    }
}
