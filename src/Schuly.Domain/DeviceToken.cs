namespace Schuly.Domain
{
    public class DeviceToken : Base
    {
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public required string Token { get; set; }
        // Lowercase strings, not enums: the app contract sends these exact values.
        public required string Platform { get; set; }
        public required string Locale { get; set; }
        public DateTime LastSeenAt { get; set; }
    }
}
