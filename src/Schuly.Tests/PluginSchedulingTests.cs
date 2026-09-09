using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Schuly.API.Extensions;
using Schuly.API.Plugins;
using Schuly.Infrastructure;
using Schuly.Plugin.Abstractions;
using Schuly.Tests.Plugin;
using Schuly.Tests.TestHelpers;
using TickerQ.Utilities;

namespace Schuly.Tests
{
    public class PluginSchedulingTests
    {
        [Test]
        public async Task Load_registers_the_plugin_schedule_and_unload_withdraws_it()
        {
            await using var h = await PluginTestHarness.StartAsync();

            await h.Host.LoadAsync(h.CopyTestPlugin(), h.Directory);

            var registrations = h.Scheduler.Synced[TestPlugin.PluginName];
            await Assert.That(registrations.Count).IsEqualTo(1);

            var registration = registrations[0];
            await Assert.That(registration.Task).IsEqualTo("test.sync");
            await Assert.That(registration.Cron).IsEqualTo("*/30 * * * *");
            await Assert.That(registration.Retries).IsEqualTo(0);
            await Assert.That(registration.RunOnStartup).IsFalse();

            await h.Host.UnloadAsync(TestPlugin.PluginName);
            await Assert.That(h.Scheduler.Removed.Contains(TestPlugin.PluginName)).IsTrue();
        }

        private sealed class RecordingTask(string name, PluginSchedule schedule) : IPluginBackgroundTask
        {
            public string Name => name;
            public PluginSchedule Schedule => schedule;
            public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private static IConfiguration ConfigFrom(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        [Test]
        public async Task Resolve_without_override_keeps_the_plugin_default()
        {
            var task = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *", Retries: 2, RunOnStartup: true));
            var config = ConfigFrom([]);

            var registration = PluginTaskScheduleResolver.Resolve("Plugin", task, config, NullLogger.Instance);

            await Assert.That(registration.Cron).IsEqualTo("0 6 * * *");
            await Assert.That(registration.Retries).IsEqualTo(2);
            await Assert.That(registration.RunOnStartup).IsTrue();
        }

        [Test]
        public async Task Resolve_applies_a_cron_override()
        {
            var task = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *"));
            var config = ConfigFrom(new Dictionary<string, string?>
            {
                ["Schedules:plugin.sync:Cron"] = "0 3 * * *",
            });

            var registration = PluginTaskScheduleResolver.Resolve("Plugin", task, config, NullLogger.Instance);

            await Assert.That(registration.Cron).IsEqualTo("0 3 * * *");
        }

        [Test]
        public async Task Resolve_ignores_an_invalid_cron_override_and_keeps_the_default()
        {
            var task = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *"));
            var config = ConfigFrom(new Dictionary<string, string?>
            {
                ["Schedules:plugin.sync:Cron"] = "not-a-cron",
            });

            var registration = PluginTaskScheduleResolver.Resolve("Plugin", task, config, NullLogger.Instance);

            await Assert.That(registration.Cron).IsEqualTo("0 6 * * *");
        }

        [Test]
        public async Task Resolve_applies_retries_and_run_on_startup_overrides()
        {
            var task = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *", Retries: 1, RunOnStartup: false));
            var config = ConfigFrom(new Dictionary<string, string?>
            {
                ["Schedules:plugin.sync:Retries"] = "5",
                ["Schedules:plugin.sync:RunOnStartup"] = "true",
            });

            var registration = PluginTaskScheduleResolver.Resolve("Plugin", task, config, NullLogger.Instance);

            await Assert.That(registration.Retries).IsEqualTo(5);
            await Assert.That(registration.RunOnStartup).IsTrue();
        }

        [Test]
        public async Task Resolve_ignores_non_numeric_retries_and_non_boolean_run_on_startup()
        {
            var task = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *", Retries: 1, RunOnStartup: false));
            var config = ConfigFrom(new Dictionary<string, string?>
            {
                ["Schedules:plugin.sync:Retries"] = "not-a-number",
                ["Schedules:plugin.sync:RunOnStartup"] = "not-a-bool",
            });

            var registration = PluginTaskScheduleResolver.Resolve("Plugin", task, config, NullLogger.Instance);

            await Assert.That(registration.Retries).IsEqualTo(1);
            await Assert.That(registration.RunOnStartup).IsFalse();
        }

        [Test]
        public async Task Resolve_override_only_applies_to_the_named_task()
        {
            var syncTask = new RecordingTask("plugin.sync", new PluginSchedule("0 6 * * *"));
            var cleanupTask = new RecordingTask("plugin.cleanup", new PluginSchedule("0 7 * * *"));
            var config = ConfigFrom(new Dictionary<string, string?>
            {
                ["Schedules:plugin.sync:Cron"] = "0 3 * * *",
            });

            var syncRegistration = PluginTaskScheduleResolver.Resolve("Plugin", syncTask, config, NullLogger.Instance);
            var cleanupRegistration = PluginTaskScheduleResolver.Resolve("Plugin", cleanupTask, config, NullLogger.Instance);

            await Assert.That(syncRegistration.Cron).IsEqualTo("0 3 * * *");
            await Assert.That(cleanupRegistration.Cron).IsEqualTo("0 7 * * *");
        }

        // TickerFunctionProvider is static/global state populated by a module initializer the
        // moment Schuly.API is touched, so this has to run alone: it is the one test that
        // actually calls TickerFunctionProvider.Build(), and interference from a second call
        // elsewhere in the same process would make the assertion meaningless.
        [Test]
        [NotInParallel]
        public async Task Host_ticker_function_is_registered_by_the_source_generator()
        {
            var services = new ServiceCollection();
            services.AddDbContext<SchulyDbContext>(options => options.UseNpgsql("Host=localhost;Database=schuly_test;Username=x;Password=y"));
            services.AddSchulyTickerQ();

            TickerFunctionProvider.Build();

            await Assert.That(TickerFunctionProvider.IsBuilt).IsTrue();
            await Assert.That(TickerFunctionProvider.TickerFunctions.ContainsKey("RunPluginTask")).IsTrue();
        }
    }
}
