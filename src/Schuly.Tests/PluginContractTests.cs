using System.Reflection;
using Schuly.API.Plugins;

namespace Schuly.Tests
{
    public class PluginContractTests
    {
        private static AssemblyName Contract(string version) => new(PluginContract.AssemblyName) { Version = Version.Parse(version) };

        [Test]
        [Arguments("0.4.0.0", "0.4.0.0", true)]
        [Arguments("0.3.0.0", "0.4.0.0", false)]
        [Arguments("0.5.0.0", "0.4.0.0", false)]
        [Arguments("1.0.0.0", "1.2.0.0", true)]
        [Arguments("1.3.0.0", "1.2.0.0", false)]
        [Arguments("1.2.0.0", "2.0.0.0", false)]
        public async Task IsCompatible_follows_the_versioning_rules(string plugin, string host, bool expected)
        {
            await Assert.That(PluginContract.IsCompatible(Version.Parse(plugin), Version.Parse(host))).IsEqualTo(expected);
        }

        [Test]
        public async Task A_plugin_built_for_an_older_contract_is_refused_with_both_versions()
        {
            var reason = PluginContract.Incompatibility([Contract("0.3.0.0")], Version.Parse("0.4.0.0"));

            await Assert.That(reason).IsNotNull();
            await Assert.That(reason!).Contains("0.3");
            await Assert.That(reason!).Contains("0.4");
        }

        [Test]
        public async Task A_plugin_referencing_backend_assemblies_is_refused()
        {
            var reason = PluginContract.Incompatibility([Contract("0.4.0.0"), new AssemblyName("Schuly.Infrastructure")], Version.Parse("0.4.0.0"));

            await Assert.That(reason).IsNotNull();
            await Assert.That(reason!).Contains("Schuly.Infrastructure");
        }

        [Test]
        public async Task A_registry_entry_for_a_newer_contract_is_not_supported_but_a_pinned_one_is_left_to_the_load_check()
        {
            var host = Version.Parse("0.4.0.0");
            var latest = new RegistryPlugin { Name = "Schuly.Plugin.Schulware", Version = "3.0.0", Contract = "0.5" };

            await Assert.That(latest.SupportsContract(host)).IsFalse();
            await Assert.That(latest.WithPinnedVersion("2.6.0").SupportsContract(host)).IsTrue();
            await Assert.That((latest with { Contract = "0.4" }).SupportsContract(host)).IsTrue();
        }

        [Test]
        public async Task A_compatible_plugin_is_accepted()
        {
            await Assert.That(PluginContract.Incompatibility([Contract("0.4.0.0"), new AssemblyName("System.Runtime")], Version.Parse("0.4.0.0"))).IsNull();
        }
    }
}
