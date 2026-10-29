namespace Schuly.Domain.Mcp
{
    /// <summary>An opaque access or refresh token for <c>/mcp</c>. Only its SHA-256 hash is stored. A token without a grant is an internal one minted for Schuly itself (Helply).</summary>
    public class McpToken : Base
    {
        public required string TokenHash { get; set; }
        public McpTokenKind Kind { get; set; }

        public Guid ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public Guid? McpGrantId { get; set; }
        public McpGrant? McpGrant { get; set; }

        /// <summary>Only for internal tokens. A token issued to a connected app reads the live permissions of its grant instead.</summary>
        public McpPermissionSet? Permissions { get; set; }
        public required string Resource { get; set; }
        public DateTime ExpiresAt { get; set; }

        /// <summary>Set when a refresh token is exchanged. Presenting it again means it was copied, and the whole grant is revoked.</summary>
        public DateTime? ConsumedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}
