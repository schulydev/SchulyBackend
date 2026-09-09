using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Vault;

namespace Schuly.Tests
{
    public class PersistentVaultTests
    {
        private static (IVaultStore Store, ServiceProvider Provider) NewStore()
        {
            // The database name must be captured once, outside the configure delegate —
            // AddDbContext re-invokes it for every scoped DbContext it constructs, so a
            // Guid.NewGuid() call inside the lambda would hand each scope its own
            // isolated, empty database instead of sharing one.
            var dbName = Guid.NewGuid().ToString();
            var services = new ServiceCollection();
            services.AddDbContext<SchulyDbContext>(o => o.UseInMemoryDatabase(dbName));
            var provider = services.BuildServiceProvider();
            var store = new DbVaultStore(provider.GetRequiredService<IServiceScopeFactory>());
            return (store, provider);
        }

        private static byte[] NewMasterKey() => RandomNumberGenerator.GetBytes(VaultKeyring.MasterKeySize);

        [Test]
        public async Task Round_trip_across_a_simulated_restart()
        {
            var (store, provider) = NewStore();
            var masterKey = NewMasterKey();

            var v1 = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            v1.Set("token", "s3cr3t-value");

            // A brand new factory/vault over the same store and the same master key
            // simulates a process restart: the cache is gone, only the DB row remains.
            var v2 = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");

            await Assert.That(v2.Get("token")).IsEqualTo("s3cr3t-value");
            provider.Dispose();
        }

        [Test]
        public async Task Persisted_row_holds_ciphertext_not_plaintext_with_correct_nonce_and_tag_lengths()
        {
            var (store, provider) = NewStore();
            const string secret = "this-must-not-appear-in-the-database";
            var vault = new PluginVaultFactory(new VaultKeyring(NewMasterKey()), store).GetVault("plugin:a");
            vault.Set("token", secret);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            var row = db.VaultEntries.Single(e => e.PluginName == "plugin:a" && e.Key == "token");

            await Assert.That(row.Nonce.Length).IsEqualTo(12);
            await Assert.That(row.Tag.Length).IsEqualTo(16);
            await Assert.That(ContainsBytes(row.Ciphertext, Encoding.UTF8.GetBytes(secret))).IsFalse();

            provider.Dispose();
        }

        [Test]
        public async Task Tampering_with_the_ciphertext_is_detected_without_throwing()
        {
            var (store, provider) = NewStore();
            var masterKey = NewMasterKey();
            var vault = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            vault.Set("k", "secret");

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                var row = db.VaultEntries.Single(e => e.PluginName == "plugin:a" && e.Key == "k");
                var tampered = (byte[])row.Ciphertext.Clone();
                tampered[0] ^= 0xFF;
                row.Ciphertext = tampered;
                db.SaveChanges();
            }

            var fresh = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            await Assert.That(fresh.Get("k")).IsNull();
            await Assert.That(fresh.TryGet("k", out var value)).IsFalse();
            await Assert.That(value).IsNull();

            provider.Dispose();
        }

        [Test]
        public async Task Tampering_with_the_tag_is_detected_without_throwing()
        {
            var (store, provider) = NewStore();
            var masterKey = NewMasterKey();
            var vault = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            vault.Set("k", "secret");

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
                var row = db.VaultEntries.Single(e => e.PluginName == "plugin:a" && e.Key == "k");
                var tampered = (byte[])row.Tag.Clone();
                tampered[0] ^= 0xFF;
                row.Tag = tampered;
                db.SaveChanges();
            }

            var fresh = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            await Assert.That(fresh.Get("k")).IsNull();
            await Assert.That(fresh.TryGet("k", out var value)).IsFalse();
            await Assert.That(value).IsNull();

            provider.Dispose();
        }

        [Test]
        public async Task A_fresh_vault_over_the_same_store_with_a_different_master_key_returns_null()
        {
            var (store, provider) = NewStore();
            var v1 = new PluginVaultFactory(new VaultKeyring(NewMasterKey()), store).GetVault("plugin:a");
            v1.Set("k", "secret");

            var v2 = new PluginVaultFactory(new VaultKeyring(NewMasterKey()), store).GetVault("plugin:a");
            await Assert.That(v2.Get("k")).IsNull();

            provider.Dispose();
        }

        [Test]
        public async Task Set_twice_then_a_fresh_vault_reads_the_newest_value()
        {
            var (store, provider) = NewStore();
            var masterKey = NewMasterKey();

            var v1 = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            v1.Set("k", "one");
            v1.Set("k", "two");

            var v2 = new PluginVaultFactory(new VaultKeyring(masterKey), store).GetVault("plugin:a");
            await Assert.That(v2.Get("k")).IsEqualTo("two");

            provider.Dispose();
        }

        [Test]
        public async Task Remove_deletes_the_row()
        {
            var (store, provider) = NewStore();
            var vault = new PluginVaultFactory(new VaultKeyring(NewMasterKey()), store).GetVault("plugin:a");
            vault.Set("k", "v");

            await Assert.That(vault.Remove("k")).IsTrue();

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            await Assert.That(db.VaultEntries.Any(e => e.PluginName == "plugin:a" && e.Key == "k")).IsFalse();

            provider.Dispose();
        }

        [Test]
        public async Task Clear_deletes_all_rows_of_the_namespace_and_leaves_other_namespaces_untouched()
        {
            var (store, provider) = NewStore();
            var factory = new PluginVaultFactory(new VaultKeyring(NewMasterKey()), store);
            var a = factory.GetVault("plugin:a");
            var b = factory.GetVault("plugin:b");
            a.Set("k1", "v1");
            a.Set("k2", "v2");
            b.Set("k3", "v3");

            a.Clear();

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SchulyDbContext>();
            await Assert.That(db.VaultEntries.Any(e => e.PluginName == "plugin:a")).IsFalse();
            await Assert.That(db.VaultEntries.Any(e => e.PluginName == "plugin:b")).IsTrue();

            provider.Dispose();
        }

        [Test]
        public async Task AddSchulyVault_throws_when_master_key_missing_outside_development()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            var configuration = ConfigWithKey(null);

            await Assert.That(() => services.AddSchulyVault(configuration, isDevelopment: false)).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task AddSchulyVault_throws_when_master_key_is_not_base64_outside_development()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            var configuration = ConfigWithKey("not-base64!");

            await Assert.That(() => services.AddSchulyVault(configuration, isDevelopment: false)).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task AddSchulyVault_throws_when_master_key_is_the_wrong_length_outside_development()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            var configuration = ConfigWithKey(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));

            await Assert.That(() => services.AddSchulyVault(configuration, isDevelopment: false)).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task AddSchulyVault_falls_back_to_an_ephemeral_key_in_development_when_missing()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IVaultStore>(NullVaultStore.Instance);
            var configuration = ConfigWithKey(null);

            services.AddSchulyVault(configuration, isDevelopment: true);
            using var provider = services.BuildServiceProvider();

            var vault = provider.GetRequiredService<IPluginVaultFactory>().GetVault("plugin:dev");
            vault.Set("k", "v");

            await Assert.That(vault.Get("k")).IsEqualTo("v");
        }

        private static IConfiguration ConfigWithKey(string? key)
        {
            var values = new Dictionary<string, string?>();
            if (key is not null)
                values["Vault:MasterKey"] = key;
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static bool ContainsBytes(byte[] haystack, byte[] needle)
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
