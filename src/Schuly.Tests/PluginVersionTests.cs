using Schuly.API.Plugins;

namespace Schuly.Tests
{
    public class PluginVersionTests
    {
        private static RegistryPlugin Entry() => new() { Name = "Schuly.Plugin.Schulware", Version = "1.0.0", Dll = "Schuly.Plugin.Schulware.dll" };

        [Test]
        public async Task WithPinnedVersion_rejects_a_path_traversal_version()
        {
            var entry = Entry();

            await Assert.That(() => entry.WithPinnedVersion("../../evil")).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task WithPinnedVersion_accepts_a_valid_semver()
        {
            var pinned = Entry().WithPinnedVersion("1.2.3-rc.1");

            await Assert.That(pinned.Dll).IsEqualTo("Schuly.Plugin.Schulware-v1.2.3-rc.1.dll");
        }

        [Test]
        public async Task WithPinnedVersion_leaves_entry_unchanged_for_latest_or_null()
        {
            var entry = Entry();

            await Assert.That(entry.WithPinnedVersion("latest")).IsEqualTo(entry);
            await Assert.That(entry.WithPinnedVersion(null)).IsEqualTo(entry);
        }
    }
}
