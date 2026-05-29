using Schuly.Plugin.Abstractions;

namespace Schuly.API.Services
{
    public class PluginBackgroundTaskHost(
        IServiceProvider serviceProvider,
        PluginSchedulerRegistry registry,
        ILogger<PluginBackgroundTaskHost> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tasks = serviceProvider.GetServices<IPluginBackgroundTask>();

            var runners = tasks.Select(task => RunTaskLoop(task, stoppingToken));
            await Task.WhenAll(runners);
        }

        private async Task RunTaskLoop(IPluginBackgroundTask task, CancellationToken stoppingToken)
        {
            logger.LogInformation("Plugin background task '{Name}' started with interval {Interval}", task.Name, task.Interval);
            registry.Register(task.Name, task.Interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                registry.RecordStart(task.Name);
                try
                {
                    await task.ExecuteAsync(serviceProvider, stoppingToken);
                    registry.RecordSuccess(task.Name, ElapsedMs(startedAt));
                }
                catch (Exception ex)
                {
                    registry.RecordFailure(task.Name, ElapsedMs(startedAt), ex.Message);
                    logger.LogError(ex, "Plugin background task '{Name}' failed", task.Name);
                }

                try
                {
                    await Task.Delay(task.Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            logger.LogInformation("Plugin background task '{Name}' stopped", task.Name);
        }

        private static long ElapsedMs(long startTimestamp) =>
            (long)System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
    }
}
