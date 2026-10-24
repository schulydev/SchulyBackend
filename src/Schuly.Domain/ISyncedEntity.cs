namespace Schuly.Domain
{
    /// <summary>A row a plugin can sync. <see cref="Source"/> is the plugin that owns it; null means entered by hand.</summary>
    public interface ISyncedEntity
    {
        string? Source { get; set; }
        string? ExternalId { get; set; }
    }
}
