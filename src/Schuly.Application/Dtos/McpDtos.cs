using Schuly.Domain.Mcp;

namespace Schuly.Application.Dtos
{
    /// <summary>What the consent screen shows about an app asking for access.</summary>
    public record McpAuthorizationRequestDto(bool Valid, string? Error, string? ErrorDescription, string? ClientName, string? ClientUri, string? RedirectHost, McpPermissionSet Requested, string? RedirectUrl);

    /// <summary>Where the browser goes after the user approved or denied: back to the app, with a code or an error.</summary>
    public record McpAuthorizationDecisionDto(string RedirectUrl);

    public record McpConnectionDto(Guid Id, string ClientName, string? ClientUri, IReadOnlyList<string> RedirectHosts, McpPermissionSet Permissions, DateTime CreatedAt, DateTime? LastUsedAt);

    public record McpApiKeyDto(Guid Id, string Name, string Prefix, McpPermissionSet Permissions, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastUsedAt);

    /// <summary>A new key. <see cref="Secret"/> is returned this once and never stored.</summary>
    public record CreatedMcpApiKeyDto(McpApiKeyDto Key, string Secret);

    public record McpActivityDto(string ToolName, bool Allowed, DateTime At);

    /// <summary>The parameters of an OAuth authorization request, passed through from /oauth/authorize to the consent page and back.</summary>
    public record McpAuthorizationParameters(string? ResponseType, string? ClientId, string? RedirectUri, string? Scope, string? State, string? CodeChallenge, string? CodeChallengeMethod, string? Resource);
}
