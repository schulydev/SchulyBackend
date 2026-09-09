using Microsoft.Extensions.Options;
using Schuly.Infrastructure.Services;

namespace Schuly.API.Services
{
    public sealed class RetentionHostedService(IServiceScopeFactory scopeFactory, IOptions<RetentionOptions> options, ILogger<RetentionHostedService> logger) : BackgroundService
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!options.Value.Enabled)
            {
                logger.LogInformation("Retention sweep is disabled; the hosted service will not run.");
                return;
            }

            try
            {
                await Task.Delay(InitialDelay, stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var sweeper = scope.ServiceProvider.GetRequiredService<RetentionSweeper>();
                        await sweeper.SweepAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Retention sweep failed.");
                    }

                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
