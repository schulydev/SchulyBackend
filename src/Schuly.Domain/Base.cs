namespace Schuly.Domain
{
    /// <summary>Common timestamped identity for all Schuly domain entities.</summary>
    public abstract class Base
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; }
    }
}
