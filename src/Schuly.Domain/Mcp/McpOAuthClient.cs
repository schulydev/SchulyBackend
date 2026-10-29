namespace Schuly.Domain.Mcp
{
    /// <summary>An OAuth client an AI app registered for itself through dynamic client registration. Public clients only: none of them holds a secret.</summary>
    public class McpOAuthClient : Base
    {
        public required string ClientId { get; set; }
        public required string ClientName { get; set; }
        public List<string> RedirectUris { get; set; } = [];
        public string? ClientUri { get; set; }
        public string? SoftwareId { get; set; }
        public string? SoftwareVersion { get; set; }

        /// <summary>SHA-256 of the registering IP address, only to cap registrations per address. Never the address itself.</summary>
        public string? RegistrationIpHash { get; set; }
    }
}
