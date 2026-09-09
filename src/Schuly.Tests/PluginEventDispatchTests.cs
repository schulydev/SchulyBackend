using Microsoft.Extensions.DependencyInjection;
using Schuly.Application.Abstractions;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    // PluginActionDescriptorChangeProvider.Instance is process-wide static state shared by
    // every PluginTestHarness, so plugin host tests must not run concurrently with each other.
    [NotInParallel("PluginHost")]
    public class PluginEventDispatchTests
    {
        [Test]
        public async Task Dispatch_reaches_handler_registered_in_the_plugin_scope()
        {
            await using var h = await PluginTestHarness.StartAsync();
            await h.Host.LoadAsync(h.CopyTestPlugin(), h.Directory);

            var dispatcher = h.Services.GetRequiredService<IPluginEventDispatcher>();
            var path = Path.Combine(h.Directory, $"event-{Guid.NewGuid():N}.txt");

            await dispatcher.DispatchAsync(path);

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo("child-di-ok");
        }

        [Test]
        public async Task Dispatch_with_no_plugin_loaded_does_not_throw()
        {
            await using var h = await PluginTestHarness.StartAsync();
            var dispatcher = h.Services.GetRequiredService<IPluginEventDispatcher>();

            await dispatcher.DispatchAsync("unused-path");
        }
    }
}
