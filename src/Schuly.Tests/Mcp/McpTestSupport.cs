using Microsoft.Extensions.Options;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Mcp;
using Toamaisutaa.Abstractions;

namespace Schuly.Tests.Mcp
{
    public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now += by;
    }

    public static class McpTestSupport
    {
        public const string Resource = "https://app.schuly.test/mcp";
        public const string Issuer = "https://app.schuly.test";
        public const string Redirect = "https://claude.ai/api/mcp/auth_callback";

        public static (McpOAuthService OAuth, McpApiKeyService Keys, MutableTimeProvider Time) Services(SchulyDbContext db, McpOAuthOptions? options = null)
        {
            var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
            var opts = Options.Create(options ?? new McpOAuthOptions());
            var oauth = new McpOAuthService(db, opts, time);
            return (oauth, new McpApiKeyService(db, oauth, opts, time), time);
        }

        /// <summary>An application user backed by a Toamaisutaa account, so security stamp and disabled checks have something to read.</summary>
        public static (ApplicationUser User, ToamaisutaaUser Account) NewUser(SchulyDbContext db)
        {
            var account = new ToamaisutaaUser { Id = Guid.NewGuid(), UserName = "student" + Guid.NewGuid().ToString("N")[..6], Email = "student@schuly.test", SecurityStamp = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var user = new ApplicationUser { Id = Guid.NewGuid(), ExternalId = account.Id.ToString(), Email = "student@schuly.test" };
            db.Set<ToamaisutaaUser>().Add(account);
            db.ApplicationUsers.Add(user);
            db.SaveChanges();
            return (user, account);
        }

        public static async Task<string> RegisterAsync(McpOAuthService oauth, params string[] redirects)
        {
            var (response, error) = await oauth.RegisterClientAsync(new ClientRegistrationRequest(redirects.Length == 0 ? [Redirect] : redirects.ToList(), "Claude", null, "none", null, null, null, null, null), "203.0.113.5");
            if (error is not null)
                throw new InvalidOperationException(error.Description);
            return response!.ClientId;
        }

        public static AuthorizationRequest Request(string clientId, string challenge, string redirect = Redirect, string? scope = null, string method = "S256") =>
            new("code", clientId, redirect, scope, "state-123", challenge, method, Resource);

        public const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk-long-enough-verifier";
    }
}
