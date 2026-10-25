namespace Schuly.Infrastructure.Services
{
    /// <summary>
    /// Wipes a deleted user's plugin-owned data (each plugin's database rows and vault
    /// secrets). The core purge reaches only host-owned tables; a plugin's own storage is
    /// reachable only through the plugin, so the API layer implements this over the plugin
    /// host. A no-op implementation is used where no plugin host is present (tests, etc.).
    /// </summary>
    public interface IPluginUserDataPurger
    {
        Task PurgeAsync(Guid applicationUserId, CancellationToken cancellationToken = default);
    }

    public sealed class NullPluginUserDataPurger : IPluginUserDataPurger
    {
        public static readonly NullPluginUserDataPurger Instance = new();
        public Task PurgeAsync(Guid applicationUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
