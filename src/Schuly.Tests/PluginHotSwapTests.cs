using System.Net;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
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
    }
}
