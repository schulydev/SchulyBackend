using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Schuly.API.Services.Notifications
{
    // Polls the notification outbox and drains it. Loops immediately after a full batch
    // instead of waiting, so a burst (e.g. an initial sync) catches up quickly.
    public sealed class NotificationDispatcherService(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatcherService> logger) : BackgroundService
    {
        private const int FullBatchSize = 100;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var processed = await DrainAsync(stoppingToken);

                    if (processed >= FullBatchSize)
                        continue;

                    await timer.WaitForNextTickAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        private async Task<int> DrainAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();
                return await dispatcher.DrainOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification dispatcher poll failed");
                return 0;
            }
        }
    }
}
