using System.Net;
using Schuly.Domain;
using Schuly.Infrastructure.Plugins;
using Schuly.Infrastructure.Vault;
using Schuly.Plugin.Abstractions;
using Schuly.Plugin.Abstractions.Linking;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    /// <summary>
    /// Reproduces the reported "linked Microsoft account is missing at first, retrying errors" at the host:
    /// link, list, link the same external account again, list. Each store call gets its own DbContext, like the
    /// per-scope context a plugin gets from HostServiceScope.
    /// </summary>
    public class LinkedAccountLinkingTests
    {
        private static readonly IPluginVaultFactory Vaults = TestVault.Factory();

        private static LinkedAccountStore Store(string db) => new(TestDb.NewContext(db), Vaults);

        private static Guid NewUser(string db)
        {
            using var ctx = TestDb.NewContext(db);
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = Guid.NewGuid().ToString(), Email = "user@schuly.dev" };
            ctx.ApplicationUsers.Add(user);
            ctx.SaveChanges();
            return user.Id;
        }

        [Test]
        public async Task Link_then_list_then_link_again_keeps_one_account_with_the_new_token()
        {
            var db = nameof(Link_then_list_then_link_again_keeps_one_account_with_the_new_token);
            var user = NewUser(db);
            var record = new LinkedAccountRecord("microsoft", "oid-1", "alex@school.ch", "Alex");

            var first = await Store(db).SaveAsync(user, record);
            await Store(db).SetSecretAsync(user, first.Id, LinkedSecrets.RefreshToken, "token-1");
            var afterFirst = await Store(db).ListAsync(user, "microsoft");

            var second = await Store(db).SaveAsync(user, record with { DisplayName = "Alex M" });
            await Store(db).SetSecretAsync(user, second.Id, LinkedSecrets.RefreshToken, "token-2");
            var afterSecond = await Store(db).ListAsync(user, "microsoft");

            await Assert.That(afterFirst.Count).IsEqualTo(1);
            await Assert.That(second.Id).IsEqualTo(first.Id);
            await Assert.That(afterSecond.Count).IsEqualTo(1);
            await Assert.That(afterSecond[0].DisplayName).IsEqualTo("Alex M");
            await Assert.That(await Store(db).GetSecretAsync(user, first.Id, LinkedSecrets.RefreshToken)).IsEqualTo("token-2");
        }

        [Test]
        public async Task An_account_is_visible_to_a_fresh_context_as_soon_as_save_returns()
        {
            var db = nameof(An_account_is_visible_to_a_fresh_context_as_soon_as_save_returns);
            var user = NewUser(db);
            var writer = Store(db);
            var reader = Store(db);

            await reader.ListAsync(user, "microsoft");
            await writer.SaveAsync(user, new LinkedAccountRecord("microsoft", "oid-2", "b@school.ch", null));

            await Assert.That((await reader.ListAsync(user, "microsoft")).Count).IsEqualTo(1);
        }

        private sealed class StoreBackedProvider(string db) : ILinkProvider
        {
            public int Links;
            public LinkProviderDescriptor Descriptor { get; } = new() { Id = "store-provider", DisplayName = "Store", Flows = [new LinkFlowDescriptor { Id = "login", Label = "Login" }] };

            public Task<LinkStepResult> StartAsync(LinkStartContext context, CancellationToken cancellationToken = default) =>
                Task.FromResult(new LinkStepResult(ScriptedLinkProvider.CredentialsStep, null));

            public async Task<LinkStepResult> SubmitAsync(LinkSubmitContext context, CancellationToken cancellationToken = default)
            {
                var store = Store(db);
                var account = await store.SaveAsync(context.ApplicationUserId, new LinkedAccountRecord("microsoft", "oid-3", context.Values["email"], "Alex"), cancellationToken);
                await store.SetSecretAsync(context.ApplicationUserId, account.Id, LinkedSecrets.RefreshToken, "token-" + Interlocked.Increment(ref Links), cancellationToken);
                return LinkStepResult.Done(account.Id.ToString(), "Alex");
            }

            public Task<LinkStepResult> PollAsync(LinkPollContext context, CancellationToken cancellationToken = default) => Task.FromResult(LinkStepResult.Fail("n/a"));

            public async Task<IReadOnlyList<LinkedAccountSummary>> ListAccountsAsync(Guid applicationUserId, CancellationToken cancellationToken = default) =>
                (await Store(db).ListAsync(applicationUserId, "microsoft", cancellationToken)).Select(a => new LinkedAccountSummary(a.Id.ToString(), a.DisplayName ?? a.Email, a.Email, LinkedAccountStatus.Ok, a.CreatedAt, null)).ToList();

            public Task<bool> UnlinkAsync(Guid applicationUserId, string accountId, CancellationToken cancellationToken = default) => Store(db).RemoveAsync(applicationUserId, Guid.Parse(accountId), cancellationToken);
        }

        [Test]
        public async Task Linking_through_the_generic_endpoints_twice_is_done_both_times_with_one_account()
        {
            var db = nameof(Linking_through_the_generic_endpoints_twice_is_done_both_times_with_one_account);
            var provider = new StoreBackedProvider(db);
            await using var host = await LinkingTestHost.StartAsync(provider);
            var user = NewUser(db);
            var body = new { stepId = "credentials", values = new Dictionary<string, string> { ["email"] = "alex@school.ch", ["password"] = "pw" } };

            async Task<(HttpStatusCode, string?)> LinkOnce()
            {
                var (_, started) = await host.Json(HttpMethod.Post, "/api/linking/providers/store-provider/start", user, new { platform = "web" });
                var (response, done) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{started.GetProperty("sessionId").GetString()}/submit", user, body);
                return (response.StatusCode, done.GetProperty("status").GetString() == "done" ? done.GetProperty("step").GetProperty("done").GetProperty("accountId").GetString() : null);
            }

            var (firstCode, firstId) = await LinkOnce();
            var (_, listAfterFirst) = await host.Json(HttpMethod.Get, "/api/linking/accounts", user);
            var (secondCode, secondId) = await LinkOnce();
            var (_, listAfterSecond) = await host.Json(HttpMethod.Get, "/api/linking/accounts", user);

            await Assert.That(firstCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(secondCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(firstId).IsNotNull();
            await Assert.That(secondId).IsEqualTo(firstId);
            await Assert.That(listAfterFirst.GetArrayLength()).IsEqualTo(1);
            await Assert.That(listAfterSecond.GetArrayLength()).IsEqualTo(1);
            await Assert.That(listAfterSecond[0].GetProperty("id").GetString()).IsEqualTo(firstId);
            await Assert.That(await Store(db).GetSecretAsync(user, Guid.Parse(firstId!), LinkedSecrets.RefreshToken)).IsEqualTo("token-2");
        }
    }
}
