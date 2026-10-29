using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Schuly.Domain.Mcp;
using Toamaisutaa.Abstractions;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>The OAuth 2.1 authorization server behind <c>/mcp</c>: public clients, authorization code with PKCE S256, rotating refresh tokens, and tokens bound to the MCP resource only.</summary>
    public class McpOAuthService(SchulyDbContext db, IOptions<McpOAuthOptions> options, TimeProvider time)
    {
        public const string AuthMethodNone = "none";
        public static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
        public static readonly string[] ResponseTypes = ["code"];

        private const int MaxRedirectUris = 10;
        private const int MaxClientNameLength = 200;
        private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(5);

        private McpOAuthOptions Options => options.Value;
        private DateTime Now => time.GetUtcNow().UtcDateTime;

        public async Task<(ClientRegistrationResponse? Response, OAuthError? Error)> RegisterClientAsync(ClientRegistrationRequest request, string? remoteIp, CancellationToken ct = default)
        {
            var redirects = request.RedirectUris ?? [];
            if (redirects.Count is 0 or > MaxRedirectUris)
                return (null, new OAuthError("invalid_redirect_uri", $"Register between 1 and {MaxRedirectUris} redirect_uris."));
            if (redirects.FirstOrDefault(r => !RedirectUris.IsAllowedForRegistration(r)) is { } bad)
                return (null, new OAuthError("invalid_redirect_uri", $"Redirect URI not allowed: {Truncate(bad, 200)}. Use https, a private-use scheme, or http on a loopback address."));
            if (request.TokenEndpointAuthMethod is { } method && method != AuthMethodNone)
                return (null, new OAuthError("invalid_client_metadata", "Only public clients are supported: token_endpoint_auth_method must be \"none\"."));
            if (request.GrantTypes is { } grants && grants.Any(g => !GrantTypes.Contains(g)))
                return (null, new OAuthError("invalid_client_metadata", "Supported grant_types are authorization_code and refresh_token."));
            if (request.ResponseTypes is { } responses && responses.Any(r => !ResponseTypes.Contains(r)))
                return (null, new OAuthError("invalid_client_metadata", "The only supported response_type is code."));
            if (request.ClientUri is { } clientUri && (!Uri.TryCreate(clientUri, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps))
                return (null, new OAuthError("invalid_client_metadata", "client_uri must be an https URL."));

            var since = Now.AddHours(-1);
            if (await db.McpOAuthClients.CountAsync(c => c.CreatedAt > since, ct) >= Options.MaxClientRegistrationsPerHour)
                return (null, new OAuthError("temporarily_unavailable", "Too many client registrations. Try again later."));

            var ipHash = string.IsNullOrEmpty(remoteIp) ? null : McpSecrets.Hash("ip:" + remoteIp);
            var day = Now.AddDays(-1);
            if (ipHash is not null && await db.McpOAuthClients.CountAsync(c => c.RegistrationIpHash == ipHash && c.CreatedAt > day, ct) >= Options.MaxClientRegistrationsPerIpPerDay)
                return (null, new OAuthError("temporarily_unavailable", "Too many client registrations from this address. Try again tomorrow."));

            var name = string.IsNullOrWhiteSpace(request.ClientName) ? "Unnamed app" : Truncate(request.ClientName.Trim(), MaxClientNameLength)!;
            var client = new McpOAuthClient
            {
                Id = Guid.NewGuid(),
                ClientId = "mcp_" + McpSecrets.Base64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18)),
                ClientName = name,
                RedirectUris = redirects.Distinct(StringComparer.Ordinal).ToList(),
                ClientUri = request.ClientUri,
                SoftwareId = Truncate(request.SoftwareId, 200),
                SoftwareVersion = Truncate(request.SoftwareVersion, 100),
                RegistrationIpHash = ipHash,
            };
            db.McpOAuthClients.Add(client);
            await db.SaveChangesAsync(ct);

            return (new ClientRegistrationResponse(client.ClientId, new DateTimeOffset(Now).ToUnixTimeSeconds(), client.ClientName, client.RedirectUris, AuthMethodNone, GrantTypes, ResponseTypes, string.Join(' ', McpScopes.Supported)), null);
        }

        /// <summary>Checks an authorization request in the order RFC 6749 section 4.1.2.1 requires: client and redirect URI first, because an error about anything else is sent to that redirect URI.</summary>
        public async Task<AuthorizationRequestCheck> CheckAuthorizationRequestAsync(AuthorizationRequest request, string expectedResource, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(request.ClientId))
                return AuthorizationRequestCheck.Fatal("invalid_request", "client_id is missing.");

            var client = await db.McpOAuthClients.AsNoTracking().SingleOrDefaultAsync(c => c.ClientId == request.ClientId, ct);
            if (client is null)
                return AuthorizationRequestCheck.Fatal("invalid_client", "This app is not registered with Schuly. Remove the server from the app and add it again.");
            if (!RedirectUris.Matches(client.RedirectUris, request.RedirectUri))
                return AuthorizationRequestCheck.Fatal("invalid_request", "The redirect_uri does not match any URI this app registered.");

            AuthorizationRequestCheck Redirect(string error, string description) => new(AuthorizationRequestStatus.Redirect, error, description, client.ClientName, client.ClientUri, request.RedirectUri, McpPermissionSet.None(), null);

            if (request.ResponseType != "code")
                return Redirect("unsupported_response_type", "Only response_type=code is supported.");
            if (request.CodeChallengeMethod != Pkce.Method || !Pkce.IsValidChallenge(request.CodeChallenge))
                return Redirect("invalid_request", "PKCE is required: send a code_challenge with code_challenge_method=S256.");
            // RFC 8707 makes resource optional for the client. There is only one resource here, so a request without one means it, and the token is bound to it either way.
            if (!string.IsNullOrEmpty(request.Resource) && !IsResource(request.Resource, expectedResource))
                return Redirect("invalid_target", $"The only resource this server issues tokens for is {expectedResource}.");

            return new AuthorizationRequestCheck(AuthorizationRequestStatus.Valid, null, null, client.ClientName, client.ClientUri, request.RedirectUri, McpScopes.Requested(request.Scope), expectedResource);
        }

        /// <summary>Records the user's approval and returns the one-time code. Re-approving an app the user already connected updates that connection instead of adding a second one.</summary>
        public async Task<string> ApproveAsync(Guid applicationUserId, AuthorizationRequest request, string resource, McpPermissionSet permissions, CancellationToken ct = default)
        {
            var client = await db.McpOAuthClients.SingleAsync(c => c.ClientId == request.ClientId, ct);
            var stamp = await SecurityStampAsync(applicationUserId, ct);

            var grant = await db.McpGrants.FirstOrDefaultAsync(g => g.ApplicationUserId == applicationUserId && g.McpOAuthClientId == client.Id && g.RevokedAt == null, ct);
            if (grant is null)
            {
                grant = new McpGrant { Id = Guid.NewGuid(), ApplicationUserId = applicationUserId, McpOAuthClientId = client.Id };
                db.McpGrants.Add(grant);
            }
            grant.Permissions = McpScopes.Normalize(permissions);
            grant.SecurityStamp = stamp;

            var code = McpSecrets.NewSecret(McpSecrets.CodePrefix);
            db.McpAuthorizationCodes.Add(new McpAuthorizationCode
            {
                Id = Guid.NewGuid(),
                CodeHash = McpSecrets.Hash(code),
                McpGrantId = grant.Id,
                RedirectUri = request.RedirectUri!,
                CodeChallenge = request.CodeChallenge!,
                Resource = resource,
                ExpiresAt = Now.Add(Options.CodeLifetime),
            });
            await db.SaveChangesAsync(ct);
            return code;
        }

        public async Task<(TokenResponse? Response, OAuthError? Error)> ExchangeCodeAsync(string? clientId, string? code, string? redirectUri, string? codeVerifier, string? resource, string expectedResource, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(clientId))
                return (null, new OAuthError("invalid_request", "code and client_id are required."));

            var hash = McpSecrets.Hash(code);
            var stored = await db.McpAuthorizationCodes.Include(c => c.McpGrant).ThenInclude(g => g!.McpOAuthClient).SingleOrDefaultAsync(c => c.CodeHash == hash, ct);
            if (stored?.McpGrant?.McpOAuthClient is not { } client || client.ClientId != clientId)
                return (null, InvalidGrant("The authorization code is invalid."));

            if (stored.ConsumedAt is not null)
            {
                // A code used twice was intercepted; whatever the first redemption issued is no longer trustworthy (RFC 6749 section 4.1.2).
                await RevokeGrantAsync(stored.McpGrant, ct);
                return (null, InvalidGrant("The authorization code was already used."));
            }

            stored.ConsumedAt = Now;
            await db.SaveChangesAsync(ct);

            if (stored.ExpiresAt <= Now)
                return (null, InvalidGrant("The authorization code expired."));
            if (!string.Equals(stored.RedirectUri, redirectUri, StringComparison.Ordinal))
                return (null, InvalidGrant("redirect_uri does not match the authorization request."));
            if (!Pkce.Verify(codeVerifier, stored.CodeChallenge))
                return (null, InvalidGrant("The code_verifier does not match the code_challenge."));
            if (resource is not null && !IsResource(resource, stored.Resource))
                return (null, new OAuthError("invalid_target", "resource does not match the authorization request."));
            if (stored.McpGrant.RevokedAt is not null)
                return (null, InvalidGrant("This connection was revoked."));
            if (!IsResource(stored.Resource, expectedResource))
                return (null, new OAuthError("invalid_target", "The code was issued for another resource."));

            return (await IssueTokensAsync(stored.McpGrant, stored.Resource, ct), null);
        }

        public async Task<(TokenResponse? Response, OAuthError? Error)> RefreshAsync(string? clientId, string? refreshToken, string? resource, string? scope, string expectedResource, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId))
                return (null, new OAuthError("invalid_request", "refresh_token and client_id are required."));

            var hash = McpSecrets.Hash(refreshToken);
            var stored = await db.McpTokens.Include(t => t.McpGrant).ThenInclude(g => g!.McpOAuthClient).SingleOrDefaultAsync(t => t.TokenHash == hash && t.Kind == McpTokenKind.Refresh, ct);
            if (stored?.McpGrant?.McpOAuthClient is not { } client || client.ClientId != clientId)
                return (null, InvalidGrant("The refresh token is invalid."));

            var grant = stored.McpGrant;
            if (stored.ConsumedAt is not null)
            {
                // Rotation means a refresh token is good once. A second use is a stolen copy racing the real client: end the connection for both.
                await RevokeGrantAsync(grant, ct);
                return (null, InvalidGrant("The refresh token was already used. Connect the app again."));
            }
            if (stored.RevokedAt is not null || grant.RevokedAt is not null)
                return (null, InvalidGrant("This connection was revoked."));
            if (stored.ExpiresAt <= Now)
                return (null, InvalidGrant("The refresh token expired. Connect the app again."));
            if (resource is not null && !IsResource(resource, stored.Resource))
                return (null, new OAuthError("invalid_target", "resource does not match the original authorization."));
            if (!IsResource(stored.Resource, expectedResource))
                return (null, new OAuthError("invalid_target", "The token was issued for another resource."));
            if (grant.SecurityStamp is not null && grant.SecurityStamp != await SecurityStampAsync(grant.ApplicationUserId, ct))
            {
                await RevokeGrantAsync(grant, ct);
                return (null, InvalidGrant("Your Schuly credentials changed since you connected this app. Connect it again."));
            }

            // A scope sent with the refresh is ignored: what the connection may do lives on the grant, and only the user changes it.
            stored.ConsumedAt = Now;
            return (await IssueTokensAsync(grant, stored.Resource, ct), null);
        }

        /// <summary>RFC 7009. Revoking a refresh token ends the whole connection; revoking an access token ends just that token. Unknown tokens are not an error.</summary>
        public async Task RevokeTokenAsync(string? token, string? clientId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(token))
                return;

            var hash = McpSecrets.Hash(token);
            var stored = await db.McpTokens.Include(t => t.McpGrant).ThenInclude(g => g!.McpOAuthClient).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
            if (stored?.McpGrant is not { } grant || (clientId is not null && grant.McpOAuthClient?.ClientId != clientId))
                return;

            if (stored.Kind == McpTokenKind.Refresh)
            {
                await RevokeGrantAsync(grant, ct);
                return;
            }

            stored.RevokedAt ??= Now;
            await db.SaveChangesAsync(ct);
        }

        /// <summary>Resolves an access token presented to <c>/mcp</c>. Null for anything not currently valid for <paramref name="expectedResource"/>.</summary>
        public async Task<McpCaller?> ValidateAccessTokenAsync(string token, string expectedResource, CancellationToken ct = default)
        {
            var hash = McpSecrets.Hash(token);
            var stored = await db.McpTokens.Include(t => t.McpGrant).Include(t => t.ApplicationUser).SingleOrDefaultAsync(t => t.TokenHash == hash && t.Kind == McpTokenKind.Access, ct);
            if (stored?.ApplicationUser is null || !McpSecrets.HashEquals(stored.TokenHash, hash) || stored.RevokedAt is not null || stored.ExpiresAt <= Now)
                return null;
            if (!IsResource(stored.Resource, expectedResource))
                return null;
            var account = await AccountAsync(stored.ApplicationUser.ExternalId, ct);
            if (account.Disabled)
                return null;
            if (stored.McpGrant is { } grant)
            {
                if (grant.RevokedAt is not null)
                    return null;
                if (grant.SecurityStamp is not null && grant.SecurityStamp != account.SecurityStamp)
                    return null;
                if (grant.LastUsedAt is null || grant.LastUsedAt < Now - LastUsedResolution)
                {
                    grant.LastUsedAt = Now;
                    await db.SaveChangesAsync(ct);
                }
            }

            return new McpCaller(stored.ApplicationUserId, stored.ApplicationUser.ExternalId, stored.McpGrant?.Permissions ?? stored.Permissions ?? McpPermissionSet.None(), stored.McpGrantId is null ? "internal" : "oauth", stored.McpGrantId, null);
        }

        /// <summary>A short-lived access token for Schuly's own MCP client (Helply), so it calls the same <c>/mcp</c> server with the same checks as any outside app. It has no refresh token and no grant, so it never shows up as a connected app.</summary>
        public async Task<string> IssueInternalAccessTokenAsync(Guid applicationUserId, McpPermissionSet permissions, TimeSpan lifetime, string resource, CancellationToken ct = default)
        {
            var token = McpSecrets.NewSecret(McpSecrets.AccessTokenPrefix);
            db.McpTokens.Add(new McpToken
            {
                Id = Guid.NewGuid(),
                TokenHash = McpSecrets.Hash(token),
                Kind = McpTokenKind.Access,
                ApplicationUserId = applicationUserId,
                Permissions = McpScopes.Normalize(permissions),
                Resource = resource,
                ExpiresAt = Now.Add(lifetime > TimeSpan.FromMinutes(15) ? TimeSpan.FromMinutes(15) : lifetime),
            });
            await db.SaveChangesAsync(ct);
            return token;
        }

        public async Task<List<McpGrantSummary>> ListGrantsAsync(Guid applicationUserId, CancellationToken ct = default)
        {
            var grants = await db.McpGrants.AsNoTracking()
                .Include(g => g.McpOAuthClient)
                .Where(g => g.ApplicationUserId == applicationUserId && g.RevokedAt == null)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync(ct);

            return grants.Select(g => new McpGrantSummary(g.Id, g.McpOAuthClient!.ClientName, g.McpOAuthClient.ClientUri, RedirectHosts(g.McpOAuthClient.RedirectUris), g.Permissions, g.CreatedAt, g.LastUsedAt)).ToList();
        }

        public async Task<bool> RevokeGrantAsync(Guid applicationUserId, Guid grantId, CancellationToken ct = default)
        {
            var grant = await db.McpGrants.SingleOrDefaultAsync(g => g.Id == grantId && g.ApplicationUserId == applicationUserId && g.RevokedAt == null, ct);
            if (grant is null)
                return false;
            await RevokeGrantAsync(grant, ct);
            return true;
        }

        /// <summary>Changes what a connected app may do. Its tokens read the grant on every request, so this applies to the next call without reconnecting.</summary>
        public async Task<McpGrantSummary?> UpdateGrantPermissionsAsync(Guid applicationUserId, Guid grantId, McpPermissionSet permissions, CancellationToken ct = default)
        {
            var grant = await db.McpGrants.Include(g => g.McpOAuthClient).SingleOrDefaultAsync(g => g.Id == grantId && g.ApplicationUserId == applicationUserId && g.RevokedAt == null, ct);
            if (grant is null)
                return null;
            grant.Permissions = McpScopes.Normalize(permissions);
            await db.SaveChangesAsync(ct);
            return new McpGrantSummary(grant.Id, grant.McpOAuthClient!.ClientName, grant.McpOAuthClient.ClientUri, RedirectHosts(grant.McpOAuthClient.RedirectUris), grant.Permissions, grant.CreatedAt, grant.LastUsedAt);
        }

        /// <summary>Deletes codes and tokens that can no longer be used, and revoked connections, so the tables hold only live credentials.</summary>
        public async Task<int> SweepAsync(CancellationToken ct = default)
        {
            var now = Now;
            var codes = await db.McpAuthorizationCodes.Where(c => c.ExpiresAt < now).ToListAsync(ct);
            var tokens = await db.McpTokens.Where(t => t.ExpiresAt < now || t.RevokedAt != null || t.ConsumedAt != null && t.ConsumedAt < now.AddDays(-1)).ToListAsync(ct);
            var grants = await db.McpGrants.Where(g => g.RevokedAt != null && g.RevokedAt < now.AddDays(-1)).ToListAsync(ct);
            var activityCutoff = now - McpActivityLog.Retention;
            var activity = await db.McpActivities.Where(a => a.CreatedAt < activityCutoff).ToListAsync(ct);
            db.McpActivities.RemoveRange(activity);
            var unusedCutoff = now - Options.UnusedClientLifetime;
            var unusedClients = await db.McpOAuthClients.Where(c => c.CreatedAt < unusedCutoff && !db.McpGrants.Any(g => g.McpOAuthClientId == c.Id)).ToListAsync(ct);
            db.McpOAuthClients.RemoveRange(unusedClients);
            db.McpAuthorizationCodes.RemoveRange(codes);
            db.McpTokens.RemoveRange(tokens);
            db.McpGrants.RemoveRange(grants);
            await db.SaveChangesAsync(ct);
            return codes.Count + tokens.Count + grants.Count + activity.Count + unusedClients.Count;
        }

        public static IReadOnlyList<string> RedirectHosts(IEnumerable<string> redirectUris) => redirectUris
            .Select(r => Uri.TryCreate(r, UriKind.Absolute, out var u) ? (u.Scheme is "http" or "https" ? u.Host : u.Scheme + "://" + u.Host) : r)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        public static bool IsResource(string? candidate, string expected) =>
            candidate is not null && string.Equals(candidate.TrimEnd('/'), expected.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

        private async Task<TokenResponse> IssueTokensAsync(McpGrant grant, string resource, CancellationToken ct)
        {
            var access = McpSecrets.NewSecret(McpSecrets.AccessTokenPrefix);
            var refresh = McpSecrets.NewSecret(McpSecrets.RefreshTokenPrefix);
            db.McpTokens.Add(new McpToken { Id = Guid.NewGuid(), TokenHash = McpSecrets.Hash(access), Kind = McpTokenKind.Access, ApplicationUserId = grant.ApplicationUserId, McpGrantId = grant.Id, Resource = resource, ExpiresAt = Now.Add(Options.AccessTokenLifetime) });
            db.McpTokens.Add(new McpToken { Id = Guid.NewGuid(), TokenHash = McpSecrets.Hash(refresh), Kind = McpTokenKind.Refresh, ApplicationUserId = grant.ApplicationUserId, McpGrantId = grant.Id, Resource = resource, ExpiresAt = Now.Add(Options.RefreshTokenLifetime) });
            grant.LastUsedAt = Now;
            await db.SaveChangesAsync(ct);
            return new TokenResponse(access, "Bearer", (long)Options.AccessTokenLifetime.TotalSeconds, refresh, McpScopes.Format(grant.Permissions));
        }

        private async Task RevokeGrantAsync(McpGrant grant, CancellationToken ct)
        {
            grant.RevokedAt ??= Now;
            var tokens = await db.McpTokens.Where(t => t.McpGrantId == grant.Id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var token in tokens)
                token.RevokedAt = Now;
            await db.SaveChangesAsync(ct);
        }

        private async Task<string?> SecurityStampAsync(Guid applicationUserId, CancellationToken ct)
        {
            var externalId = await db.ApplicationUsers.Where(u => u.Id == applicationUserId).Select(u => u.ExternalId).SingleOrDefaultAsync(ct);
            return (await AccountAsync(externalId, ct)).SecurityStamp;
        }

        /// <summary>The sign-in account behind an application user. Users without a local Toamaisutaa account (an outside identity provider) have no stamp and are never disabled here.</summary>
        internal async Task<(string? SecurityStamp, bool Disabled)> AccountAsync(string? externalId, CancellationToken ct)
        {
            if (!Guid.TryParse(externalId, out var toamaisutaaId))
                return (null, false);
            var user = await db.Set<ToamaisutaaUser>().AsNoTracking().Where(u => u.Id == toamaisutaaId).Select(u => new { u.SecurityStamp, u.DisabledAt }).SingleOrDefaultAsync(ct);
            return (user?.SecurityStamp, user?.DisabledAt is not null);
        }

        private static OAuthError InvalidGrant(string description) => new("invalid_grant", description);

        private static string? Truncate(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
    }
}
