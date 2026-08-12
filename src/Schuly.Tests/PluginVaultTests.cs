using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Schuly.Infrastructure.Vault;

namespace Schuly.Tests
{
    public class PluginVaultTests
    {
        private static IPluginVaultFactory NewFactory() => new PluginVaultFactory(new VaultKeyring());

        [Test]
        public async Task Set_then_get_round_trips()
        {
            var vault = NewFactory().GetVault("plugin:a");
            vault.Set("token", "s3cr3t-value");

            await Assert.That(vault.Get("token")).IsEqualTo("s3cr3t-value");
            await Assert.That(vault.Contains("token")).IsTrue();
            await Assert.That(vault.Count).IsEqualTo(1);
        }

        [Test]
        public async Task Missing_key_returns_null_and_false()
        {
            var vault = NewFactory().GetVault("plugin:a");

            await Assert.That(vault.Get("nope")).IsNull();
            await Assert.That(vault.TryGet("nope", out var v)).IsFalse();
            await Assert.That(v).IsNull();
            await Assert.That(vault.Contains("nope")).IsFalse();
        }

        [Test]
        public async Task Set_overwrites_and_remove_clears()
        {
            var vault = NewFactory().GetVault("plugin:a");
            vault.Set("k", "one");
            vault.Set("k", "two");
            await Assert.That(vault.Get("k")).IsEqualTo("two");

            await Assert.That(vault.Remove("k")).IsTrue();
            await Assert.That(vault.Contains("k")).IsFalse();
            await Assert.That(vault.Remove("k")).IsFalse();
        }

        [Test]
        public async Task Same_namespace_returns_same_vault_distinct_namespaces_are_independent()
        {
            var factory = NewFactory();
            var a1 = factory.GetVault("plugin:a");
            var a2 = factory.GetVault("plugin:a");
            var b = factory.GetVault("plugin:b");

            await Assert.That(ReferenceEquals(a1, a2)).IsTrue();

            a1.Set("shared-key", "from-a");
            await Assert.That(b.Contains("shared-key")).IsFalse();
            await Assert.That(b.Get("shared-key")).IsNull();
            await Assert.That(a2.Get("shared-key")).IsEqualTo("from-a");
        }

        [Test]
        public async Task Keyring_keys_are_stable_per_namespace_and_distinct_across_namespaces()
        {
            var keyring = new VaultKeyring();

            await Assert.That(keyring.DeriveKey("x").SequenceEqual(keyring.DeriveKey("x"))).IsTrue();
            await Assert.That(keyring.DeriveKey("x").SequenceEqual(keyring.DeriveKey("y"))).IsFalse();
        }

        [Test]
        public async Task A_fresh_keyring_cannot_decrypt_another_keyrings_values()
        {
            // Simulates a process restart (new master secret): the stored ciphertext
            // is unreadable, proving values are bound to the host's startup secret.
            var v1 = new PluginVaultFactory(new VaultKeyring()).GetVault("plugin:a");
            v1.Set("k", "secret");
            var blob = ReadStore(v1)["k"];

            var v2 = new PluginVaultFactory(new VaultKeyring()).GetVault("plugin:a");
            ReadStore(v2)["k"] = blob; // inject the other process's ciphertext

            await Assert.That(() => v2.Get("k")).Throws<CryptographicException>();
        }

        [Test]
        public async Task Stored_bytes_are_ciphertext_not_plaintext()
        {
            // The core guarantee: reading the backing store (a heap/pointer dump) only
            // ever exposes ciphertext — the plaintext is never present at rest.
            const string secret = "this-must-not-appear-in-memory";
            var vault = NewFactory().GetVault("plugin:a");
            vault.Set("token", secret);

            var needle = Encoding.UTF8.GetBytes(secret);
            var anyPlaintext = ReadStore(vault).Values.Any(b => Contains(b, needle));

            await Assert.That(anyPlaintext).IsFalse();
        }

        private static ConcurrentDictionary<string, byte[]> ReadStore(IPluginVault vault)
        {
            var field = vault.GetType().GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (ConcurrentDictionary<string, byte[]>)field.GetValue(vault)!;
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return false;
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
