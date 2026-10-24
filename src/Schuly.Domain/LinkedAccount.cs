namespace Schuly.Domain
{
    /// <summary>An account the user linked with an outside identity provider; its secrets live in the vault.</summary>
    public class LinkedAccount : Base
    {
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public required string Provider { get; set; }
        public required string ExternalId { get; set; }
        public required string Email { get; set; }
        public string? DisplayName { get; set; }
    }
}
