using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCrontab;
using Schuly.Infrastructure;
using Schuly.Plugin.Abstractions;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;

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

    public sealed class TickerQPluginTaskScheduler(IServiceScopeFactory scopeFactory, ILogger<TickerQPluginTaskScheduler> logger) : IPluginTaskScheduler
    {
        // Must match the [TickerFunction] name on PluginTaskRunner.RunAsync.
        private const string FunctionName = "RunPluginTask";

        public async Task SyncAsync(string plugin, IReadOnlyList<PluginTaskRegistration> tasks, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cronManager = scope.ServiceProvider.GetRequiredService<ICronTickerManager<CronTickerEntity>>();
                var timeManager = scope.ServiceProvider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();

                var prefix = $"{plugin}/";
                var existing = await db.Set<CronTickerEntity>()
                    .Where(t => t.Function == FunctionName && t.Description.StartsWith(prefix))
                    .ToListAsync(cancellationToken);

                var keys = new HashSet<string>(StringComparer.Ordinal);

                foreach (var registration in tasks)
                {
                    var key = $"{plugin}/{registration.Task}";
                    keys.Add(key);

                    var request = TickerHelper.CreateTickerRequest(new PluginTaskRequest(registration.Plugin, registration.Task));
                    var retryIntervals = registration.RetryIntervals?.Select(i => (int)i.TotalSeconds).ToArray();
                    var match = existing.FirstOrDefault(t => t.Description == key);

                    if (match is not null)
                    {
                        match.Expression = registration.Cron;
                        match.Request = request;
                        match.Retries = registration.Retries;
                        match.RetryIntervals = retryIntervals;
                        match.IsEnabled = true;

                        var updateResult = await cronManager.UpdateAsync(match, cancellationToken);
                        if (!updateResult.IsSucceeded)
                            logger.LogWarning(updateResult.Exception, "Failed to update scheduled ticker for plugin task {Key}", key);
                    }
                    else
                    {
                        var entity = new CronTickerEntity
                        {
                            Function = FunctionName,
                            Description = key,
                            Expression = registration.Cron,
                            Request = request,
                            Retries = registration.Retries,
                            RetryIntervals = retryIntervals,
                            IsEnabled = true,
                        };

                        var addResult = await cronManager.AddAsync(entity, cancellationToken);
                        if (!addResult.IsSucceeded)
                            logger.LogWarning(addResult.Exception, "Failed to add scheduled ticker for plugin task {Key}", key);
                    }
                }

                foreach (var stale in existing.Where(t => !keys.Contains(t.Description)))
                {
                    var deleteResult = await cronManager.DeleteAsync(stale.Id, cancellationToken);
                    if (!deleteResult.IsSucceeded)
                        logger.LogWarning(deleteResult.Exception, "Failed to delete stale scheduled ticker {Description}", stale.Description);
                }

                foreach (var registration in tasks.Where(t => t.RunOnStartup))
                {
                    var key = $"{plugin}/{registration.Task}";
                    var startup = new TimeTickerEntity
                    {
                        Function = FunctionName,
                        Description = key,
                        Request = TickerHelper.CreateTickerRequest(new PluginTaskRequest(registration.Plugin, registration.Task)),
                        ExecutionTime = DateTime.UtcNow,
                    };

                    var addResult = await timeManager.AddAsync(startup, cancellationToken);
                    if (!addResult.IsSucceeded)
                        logger.LogWarning(addResult.Exception, "Failed to schedule startup run for plugin task {Key}", key);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync scheduled tickers for plugin {Plugin}", plugin);
            }
        }

        public async Task RemoveAsync(string plugin, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cronManager = scope.ServiceProvider.GetRequiredService<ICronTickerManager<CronTickerEntity>>();
                var timeManager = scope.ServiceProvider.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>();
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();

                var prefix = $"{plugin}/";

                var crons = await db.Set<CronTickerEntity>()
                    .Where(t => t.Function == FunctionName && t.Description.StartsWith(prefix))
                    .ToListAsync(cancellationToken);
                foreach (var ticker in crons)
                {
                    var result = await cronManager.DeleteAsync(ticker.Id, cancellationToken);
                    if (!result.IsSucceeded)
                        logger.LogWarning(result.Exception, "Failed to delete scheduled ticker {Description} for plugin {Plugin}", ticker.Description, plugin);
                }

                var pendingTimers = await db.Set<TimeTickerEntity>()
                    .Where(t => t.Function == FunctionName && t.Description.StartsWith(prefix) && (t.Status == TickerStatus.Idle || t.Status == TickerStatus.Queued))
                    .ToListAsync(cancellationToken);
                foreach (var ticker in pendingTimers)
                {
                    var result = await timeManager.DeleteAsync(ticker.Id, cancellationToken);
                    if (!result.IsSucceeded)
                        logger.LogWarning(result.Exception, "Failed to delete pending startup ticker {Description} for plugin {Plugin}", ticker.Description, plugin);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove scheduled tickers for plugin {Plugin}", plugin);
            }
        }

        public async Task<IReadOnlyList<PluginTaskStatus>> SnapshotAsync(CancellationToken cancellationToken = default)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();

            var tickers = await db.Set<CronTickerEntity>()
                .AsNoTracking()
                .Where(t => t.Function == FunctionName)
                .ToListAsync(cancellationToken);

            var tickerIds = tickers.Select(t => t.Id).ToList();

            // TickerQ only retains a rolling window of occurrences, so the totals below are over retained history, not all time.
            var occurrencesByTicker = (await db.Set<CronTickerOccurrenceEntity<CronTickerEntity>>()
                .AsNoTracking()
                .Where(o => tickerIds.Contains(o.CronTickerId))
                .ToListAsync(cancellationToken))
                .ToLookup(o => o.CronTickerId);

            var now = DateTime.UtcNow;

            return tickers
                .Select(t => BuildStatus(t, occurrencesByTicker[t.Id], now))
                .OrderBy(s => s.Plugin, StringComparer.Ordinal)
                .ThenBy(s => s.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static PluginTaskStatus BuildStatus(CronTickerEntity ticker, IEnumerable<CronTickerOccurrenceEntity<CronTickerEntity>> occurrences, DateTime now)
        {
            var separator = ticker.Description.IndexOf('/');
            var plugin = separator >= 0 ? ticker.Description[..separator] : ticker.Description;
            var name = separator >= 0 ? ticker.Description[(separator + 1)..] : string.Empty;

            var schedule = CrontabSchedule.TryParse(ticker.Expression);
            var orderedOccurrences = occurrences.OrderByDescending(o => o.ExecutionTime).ToList();

            var lastStarted = orderedOccurrences.FirstOrDefault(o => o.Status != TickerStatus.Idle && o.Status != TickerStatus.Queued);
            var lastStatus = lastStarted is null ? "Pending" : MapStatus(lastStarted.Status);
            var lastStartedAt = lastStarted is null ? (DateTime?)null : lastStarted.LockedAt ?? lastStarted.ExecutionTime;
            var lastFinishedAt = lastStarted?.ExecutedAt;
            var lastDurationMs = lastStarted?.ExecutedAt is not null ? lastStarted.ElapsedTime : (long?)null;
            var lastError = lastStarted?.Status == TickerStatus.Failed ? lastStarted.ExceptionMessage : null;

            var nextMaterialised = orderedOccurrences
                .Where(o => (o.Status == TickerStatus.Idle || o.Status == TickerStatus.Queued) && o.ExecutionTime > now)
                .OrderBy(o => o.ExecutionTime)
                .FirstOrDefault();
            var nextRunAt = nextMaterialised?.ExecutionTime ?? schedule?.GetNextOccurrence(now);

            var finished = orderedOccurrences.Where(o => o.ExecutedAt is not null).ToList();
            var totalRuns = finished.Count;
            var totalFailures = orderedOccurrences.Count(o => o.Status == TickerStatus.Failed);
            var consecutiveFailures = finished.TakeWhile(o => o.Status == TickerStatus.Failed).Count();

            var intervalSeconds = 0d;
            if (schedule is not null)
            {
                var next1 = schedule.GetNextOccurrence(now);
                var next2 = schedule.GetNextOccurrence(next1);
                intervalSeconds = (next2 - next1).TotalSeconds;
            }

            return new PluginTaskStatus(plugin, name, ticker.Expression, intervalSeconds, lastStatus, lastStartedAt, lastFinishedAt, lastDurationMs, lastError, nextRunAt, totalRuns, totalFailures, consecutiveFailures);
        }

        private static string MapStatus(TickerStatus status) => status switch
        {
            TickerStatus.Idle or TickerStatus.Queued => "Pending",
            TickerStatus.InProgress => "Running",
            TickerStatus.Done or TickerStatus.DueDone => "Success",
            TickerStatus.Failed => "Failed",
            TickerStatus.Cancelled => "Cancelled",
            TickerStatus.Skipped => "Skipped",
            _ => "Pending",
        };
    }
}
