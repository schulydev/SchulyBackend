using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Schuly.Infrastructure.Vault;

namespace Schuly.Tests
{
    /// <summary>
    /// End-to-end "can an attacker read it out of memory?" probes. We write a secret,
    /// then go after the backing store the way someone poking at process memory would —
    /// pinning the stored object and reading it back through a raw pointer — and confirm
    /// only ciphertext comes out.
    /// </summary>
    public class PluginVaultMemoryProbeTests
    {
        [Test]
        public async Task Reading_the_stored_blob_through_a_raw_pointer_yields_ciphertext_not_plaintext()
        {
            const string secret = "PointerProbe!! top-secret token 0xCAFEBABE";
            var vault = new PluginVaultFactory(new VaultKeyring()).GetVault("plugin:probe");
            vault.Set("api-token", secret);

            // Reach the real stored byte[] and pin it, so we have a stable native
            // address — then read every byte back through that pointer with Marshal,
            // exactly as a memory-scraper would.
            var blob = ReadStore(vault)["api-token"];
            var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var viaPointer = new byte[blob.Length];
                for (var i = 0; i < blob.Length; i++)
                    viaPointer[i] = Marshal.ReadByte(ptr, i);

                // What the pointer exposes is exactly the at-rest ciphertext...
                await Assert.That(viaPointer.SequenceEqual(blob)).IsTrue();
                // ...and the plaintext secret is nowhere in it.
                await Assert.That(Contains(viaPointer, Encoding.UTF8.GetBytes(secret))).IsFalse();
            }
            finally
            {
                handle.Free();
            }
        }

        [Test]
        public async Task The_same_value_encrypts_to_different_bytes_each_write()
        {
            // A fresh random nonce per write means identical plaintext never produces
            // identical ciphertext — so the store leaks nothing by comparison either.
            var vault = new PluginVaultFactory(new VaultKeyring()).GetVault("plugin:probe");

            vault.Set("k", "identical-value");
            var first = (byte[])ReadStore(vault)["k"].Clone();
            vault.Set("k", "identical-value");
            var second = ReadStore(vault)["k"];

            await Assert.That(first.SequenceEqual(second)).IsFalse();
            await Assert.That(vault.Get("k")).IsEqualTo("identical-value");
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
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                if (match) return true;
            }
            return false;
        }
    }
}
