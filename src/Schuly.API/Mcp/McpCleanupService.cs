using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    /// <summary>Hourly removal of expired, used and revoked MCP codes and tokens.</summary>
    public sealed class McpCleanupService(IServiceScopeFactory scopes, ILogger<McpCleanupService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            try
            {
                await RunAsync(timer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task RunAsync(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<McpOAuthService>().SweepAsync(stoppingToken);
                    if (removed > 0)
                        logger.LogInformation("Removed {Count} expired MCP credentials", removed);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "MCP credential cleanup failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
