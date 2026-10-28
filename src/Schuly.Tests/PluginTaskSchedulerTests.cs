using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schuly.API.Plugins;
using Schuly.Infrastructure;

namespace Schuly.Tests
{
    public class PluginTaskSchedulerTests
    {
        private sealed class FakeRunner : IPluginTaskRunner
        {
            public List<string> Runs { get; } = [];
            public int FailuresLeft { get; set; }

            public Task RunTaskAsync(string pluginName, string taskName, CancellationToken cancellationToken = default)
            {
                lock (Runs)
                    Runs.Add($"{pluginName}/{taskName}");
                if (FailuresLeft-- > 0)
                    throw new InvalidOperationException("boom");
                return Task.CompletedTask;
            }
        }

        private static (PluginTaskScheduler Scheduler, FakeRunner Runner, IServiceProvider Services) Create()
        {
            var databaseName = $"plugin-tasks-{Guid.NewGuid():N}";
            var runner = new FakeRunner();
            var services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<SchulyDbContext>(options => options.UseInMemoryDatabase(databaseName))
                .AddSingleton<IPluginTaskRunner>(runner)
                .AddSingleton<PluginTaskScheduler>()
                .BuildServiceProvider();
            return (services.GetRequiredService<PluginTaskScheduler>(), runner, services);
        }

        private static PluginTaskRegistration Registration(string name, string cron = "*/30 * * * *", int retries = 0, bool runOnStartup = false) => new("Plugin", name, cron, retries, [TimeSpan.Zero], runOnStartup);

        [Test]
        public async Task Sync_stores_tasks_and_drops_the_ones_a_plugin_no_longer_has()
        {
            var (scheduler, _, _) = Create();

            await scheduler.SyncAsync("Plugin", [Registration("sync"), Registration("cleanup", "0 3 * * *")]);
            var first = await scheduler.SnapshotAsync();
            await scheduler.SyncAsync("Plugin", [Registration("sync")]);
            var second = await scheduler.SnapshotAsync();

            await Assert.That(first.Select(s => s.Name)).IsEquivalentTo(["cleanup", "sync"]);
            var sync = first.Single(s => s.Name == "sync");
            await Assert.That(sync.Cron).IsEqualTo("*/30 * * * *");
            await Assert.That(sync.IntervalSeconds).IsEqualTo(1800);
            await Assert.That(sync.NextRunAt).IsNotNull();
            await Assert.That(sync.LastStatus).IsEqualTo("Pending");
            await Assert.That(second.Select(s => s.Name)).IsEquivalentTo(["sync"]);
        }

        [Test]
        public async Task Run_on_startup_runs_once_and_then_waits_for_the_cron()
        {
            var (scheduler, runner, _) = Create();
            await scheduler.SyncAsync("Plugin", [Registration("sync", runOnStartup: true), Registration("later")]);

            var now = DateTime.UtcNow.AddSeconds(1);
            await scheduler.RunDueAsync(now);
            await scheduler.RunDueAsync(now);

            var status = (await scheduler.SnapshotAsync()).Single(s => s.Name == "sync");
            await Assert.That(runner.Runs).IsEquivalentTo(["Plugin/sync"]);
            await Assert.That(status.LastStatus).IsEqualTo("Success");
            await Assert.That(status.TotalRuns).IsEqualTo(1);
            await Assert.That(status.LastFinishedAt).IsNotNull();
            await Assert.That(status.NextRunAt!.Value).IsGreaterThan(now);
        }

        [Test]
        public async Task Failed_runs_are_retried_and_counted()
        {
            var (scheduler, runner, _) = Create();
            runner.FailuresLeft = 10;
            await scheduler.SyncAsync("Plugin", [Registration("sync", retries: 2, runOnStartup: true)]);

            await scheduler.RunDueAsync(DateTime.UtcNow.AddSeconds(1));

            var status = (await scheduler.SnapshotAsync()).Single();
            await Assert.That(runner.Runs.Count).IsEqualTo(3);
            await Assert.That(status.LastStatus).IsEqualTo("Failed");
            await Assert.That(status.LastError).IsEqualTo("boom");
            await Assert.That(status.TotalRuns).IsEqualTo(1);
            await Assert.That(status.TotalFailures).IsEqualTo(1);
            await Assert.That(status.ConsecutiveFailures).IsEqualTo(1);
        }

        [Test]
        public async Task A_retry_that_succeeds_resets_the_failure_streak()
        {
            var (scheduler, runner, _) = Create();
            runner.FailuresLeft = 1;
            await scheduler.SyncAsync("Plugin", [Registration("sync", retries: 1, runOnStartup: true)]);

            await scheduler.RunDueAsync(DateTime.UtcNow.AddSeconds(1));

            var status = (await scheduler.SnapshotAsync()).Single();
            await Assert.That(runner.Runs.Count).IsEqualTo(2);
            await Assert.That(status.LastStatus).IsEqualTo("Success");
            await Assert.That(status.TotalFailures).IsEqualTo(0);
            await Assert.That(status.ConsecutiveFailures).IsEqualTo(0);
        }

        [Test]
        public async Task Removed_plugins_stop_running_and_leave_no_rows()
        {
            var (scheduler, runner, services) = Create();
            await scheduler.SyncAsync("Plugin", [Registration("sync", runOnStartup: true)]);

            await scheduler.RemoveAsync("Plugin");
            await scheduler.RunDueAsync(DateTime.UtcNow.AddSeconds(1));

            using var scope = services.CreateScope();
            await Assert.That(runner.Runs).IsEmpty();
            await Assert.That(await scope.ServiceProvider.GetRequiredService<SchulyDbContext>().PluginTasks.CountAsync()).IsEqualTo(0);
        }

        [Test]
        public async Task A_run_missed_while_the_server_was_down_runs_after_the_next_start()
        {
            var (scheduler, runner, services) = Create();
            await scheduler.SyncAsync("Plugin", [Registration("sync")]);
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                (await db.PluginTasks.SingleAsync()).NextRunAt = DateTime.UtcNow.AddHours(-1);
                await db.SaveChangesAsync();
            }

            var restarted = services.GetRequiredService<IServiceScopeFactory>();
            var afterRestart = new PluginTaskScheduler(restarted, services, Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginTaskScheduler>.Instance);
            await afterRestart.SyncAsync("Plugin", [Registration("sync")]);
            await afterRestart.RunDueAsync(DateTime.UtcNow.AddSeconds(1));

            await Assert.That(runner.Runs).IsEquivalentTo(["Plugin/sync"]);
        }

        [Test]
        public async Task Cron_with_a_question_mark_is_accepted()
        {
            await Assert.That(PluginTaskScheduler.ParseCron("0 7 ? * MON")).IsNotNull();
        }
    }
}
