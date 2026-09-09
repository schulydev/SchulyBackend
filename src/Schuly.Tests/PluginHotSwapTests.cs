using System.Net;
using Schuly.Tests.TestHelpers;
using TestPlugin = Schuly.Tests.Plugin.TestPlugin;

namespace Schuly.Tests
{
    // PluginActionDescriptorChangeProvider.Instance is process-wide static state shared by
    // every PluginTestHarness, so hot-swap tests must not run concurrently with each other.
    [NotInParallel("PluginHost")]
    public class PluginHotSwapTests
    {
        [Test]
        public async Task Endpoints_and_controllers_hot_load_and_unload()
        {
            await using var h = await PluginTestHarness.StartAsync();

            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(await h.Status("/api/plugins/test/controller-ping")).IsEqualTo(HttpStatusCode.NotFound);

            await h.Host.LoadAsync(h.CopyTestPlugin(), h.Directory);

            var ping = await h.Client.GetAsync("/api/plugins/test/ping");
            await Assert.That(ping.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await ping.Content.ReadAsStringAsync()).Contains("pong");

            var ctrl = await h.Client.GetAsync("/api/plugins/test/controller-ping");
            await Assert.That(ctrl.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(await ctrl.Content.ReadAsStringAsync()).Contains("child-di-ok");

            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsTrue();

            await h.Host.UnloadAsync("Test Plugin");
            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(await h.Status("/api/plugins/test/controller-ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsFalse();
        }

        [Test]
        public async Task Failed_load_rolls_back_and_leaves_a_clean_slate()
        {
            await using var h = await PluginTestHarness.StartAsync();
            var manifest = h.CopyTestPlugin();
            var markerPath = Path.Combine(h.Directory, TestPlugin.FailMigrateMarker);

            File.WriteAllText(markerPath, "");

            await Assert.That(async () => await h.Host.LoadAsync(manifest, h.Directory))
                .Throws<InvalidOperationException>();
            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsFalse();
            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(File.Exists(Path.Combine(h.Directory, TestPlugin.ProviderDisposedMarker))).IsTrue();

            File.Delete(markerPath);

            await h.Host.LoadAsync(manifest, h.Directory);
            await Assert.That(h.Host.IsLoaded("Test Plugin")).IsTrue();
            await Assert.That(await h.Status("/api/plugins/test/ping")).IsEqualTo(HttpStatusCode.OK);
        }
    }
}
