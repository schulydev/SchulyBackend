using Schuly.Domain.Mcp;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    /// <summary>
    /// The hook for Schuly's own MCP client (Helply). It mints a short-lived access token for the signed-in user with the permission set Helply was given, so Helply
    /// talks to the same <c>/mcp</c> through the official SDK client and gets exactly the tools, checks and rate limits any outside app gets.
    /// </summary>
    public sealed class McpInternalTokens(McpOAuthService oauth, McpPublicUrls urls)
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        /// <summary>The bearer token and the absolute <c>/mcp</c> URL it is valid for.</summary>
        public async Task<(string AccessToken, Uri Endpoint)> IssueAsync(Guid applicationUserId, McpPermissionSet permissions, CancellationToken ct = default)
        {
            var token = await oauth.IssueInternalAccessTokenAsync(applicationUserId, permissions, Lifetime, urls.Resource, ct);
            return (token, new Uri(urls.Resource));
        }
    }
}
