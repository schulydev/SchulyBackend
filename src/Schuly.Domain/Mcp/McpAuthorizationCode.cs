namespace Schuly.Domain.Mcp
{
    /// <summary>A single-use authorization code. Only its SHA-256 hash is stored.</summary>
    public class McpAuthorizationCode : Base
    {
        public required string CodeHash { get; set; }
        public Guid McpGrantId { get; set; }
        public McpGrant? McpGrant { get; set; }
        public required string RedirectUri { get; set; }
        public required string CodeChallenge { get; set; }
        public required string Resource { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
    }
}
