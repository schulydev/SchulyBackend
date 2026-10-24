using System.Security.Cryptography;
using Schuly.Infrastructure.Vault;

namespace Schuly.Tests.TestHelpers
{
    public static class TestVault
    {
        /// <summary>An in-memory vault factory; one instance keeps its values for the test's lifetime.</summary>
        public static PluginVaultFactory Factory() => new(new VaultKeyring(RandomNumberGenerator.GetBytes(VaultKeyring.MasterKeySize)), NullVaultStore.Instance);
    }
}
