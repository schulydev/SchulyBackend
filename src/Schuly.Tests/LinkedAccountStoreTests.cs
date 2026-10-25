using Microsoft.Extensions.Logging.Abstractions;
using Schuly.Domain;
using Schuly.Infrastructure.Plugins;
using Schuly.Infrastructure.Services;
using Schuly.Plugin.Abstractions;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class LinkedAccountStoreTests
    {
        private static readonly LinkedAccountRecord Alex = new(LinkedProviders.Microsoft, "oid-1", "alex@school.ch", "Alex");

        private static Guid NewUser(Infrastructure.SchulyDbContext ctx)
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = Guid.NewGuid().ToString(), Email = "user@schuly.dev" };
            ctx.ApplicationUsers.Add(user);
            ctx.SaveChanges();
            return user.Id;
        }

        [Test]
        public async Task Linking_the_same_provider_identity_twice_updates_one_account()
        {
            using var ctx = TestDb.NewContext(nameof(Linking_the_same_provider_identity_twice_updates_one_account));
            var store = new LinkedAccountStore(ctx, TestVault.Factory());
            var user = NewUser(ctx);

            var first = await store.SaveAsync(user, Alex);
            var second = await store.SaveAsync(user, Alex with { Email = "alex.new@school.ch" });

            await Assert.That(second.Id).IsEqualTo(first.Id);
            var listed = await store.ListAsync(user, LinkedProviders.Microsoft);
            await Assert.That(listed.Count).IsEqualTo(1);
            await Assert.That(listed[0].Email).IsEqualTo("alex.new@school.ch");
        }

        [Test]
        public async Task Another_user_can_neither_see_nor_read_the_account()
        {
            using var ctx = TestDb.NewContext(nameof(Another_user_can_neither_see_nor_read_the_account));
            var store = new LinkedAccountStore(ctx, TestVault.Factory());
            var owner = NewUser(ctx);
            var other = NewUser(ctx);
            var account = await store.SaveAsync(owner, Alex);
            await store.SetSecretAsync(owner, account.Id, LinkedSecrets.SessionCookies, "[]");

            await Assert.That(await store.GetAsync(other, account.Id)).IsNull();
            await Assert.That(await store.GetSecretAsync(other, account.Id, LinkedSecrets.SessionCookies)).IsNull();
            await Assert.That((await store.ListAsync(other, LinkedProviders.Microsoft)).Count).IsEqualTo(0);
            await Assert.That(await store.RemoveAsync(other, account.Id)).IsFalse();
            await Assert.That(async () => await store.SetSecretAsync(other, account.Id, LinkedSecrets.SessionCookies, "x")).Throws<InvalidOperationException>();
        }

        [Test]
        public async Task Removing_an_account_deletes_every_secret_it_stored()
        {
            using var ctx = TestDb.NewContext(nameof(Removing_an_account_deletes_every_secret_it_stored));
            var vaults = TestVault.Factory();
            var store = new LinkedAccountStore(ctx, vaults);
            var user = NewUser(ctx);
            var account = await store.SaveAsync(user, Alex);
            await store.SetSecretAsync(user, account.Id, LinkedSecrets.RefreshToken, "rt");
            await store.SetSecretAsync(user, account.Id, LinkedSecrets.SessionCookies, "[]");
            await store.SetSecretAsync(user, account.Id, "custom", "v");

            await Assert.That(await store.RemoveAsync(user, account.Id)).IsTrue();

            await Assert.That(vaults.GetVault(LinkedAccountStore.VaultNamespace).Count).IsEqualTo(0);
            await Assert.That(ctx.LinkedAccounts.Any()).IsFalse();
        }

        [Test]
        public async Task Deleting_the_user_purges_linked_accounts_and_their_secrets()
        {
            using var ctx = TestDb.NewContext(nameof(Deleting_the_user_purges_linked_accounts_and_their_secrets));
            var vaults = TestVault.Factory();
            var store = new LinkedAccountStore(ctx, vaults);
            var user = NewUser(ctx);
            var keep = NewUser(ctx);
            var account = await store.SaveAsync(user, Alex);
            await store.SetSecretAsync(user, account.Id, LinkedSecrets.SessionCookies, "[]");
            var kept = await store.SaveAsync(keep, Alex);
            await store.SetSecretAsync(keep, kept.Id, LinkedSecrets.SessionCookies, "[]");

            await new AccountPurger(ctx, new FakeDocumentStorage(), vaults, NullPluginUserDataPurger.Instance, NullLogger<AccountPurger>.Instance).PurgeApplicationUserAsync(user);

            await Assert.That(ctx.LinkedAccounts.Any(a => a.ApplicationUserId == user)).IsFalse();
            await Assert.That(await store.GetSecretAsync(keep, kept.Id, LinkedSecrets.SessionCookies)).IsEqualTo("[]");
            await Assert.That(vaults.GetVault(LinkedAccountStore.VaultNamespace).Get($"{account.Id:N}:{LinkedSecrets.SessionCookies}")).IsNull();
        }
    }
}
