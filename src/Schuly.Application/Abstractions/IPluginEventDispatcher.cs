namespace Schuly.Application.Abstractions
{
    /// <summary>
    /// Fans a completed host message out to plugin-registered <c>IPluginEventHandler</c>s.
    /// Implemented in the API layer over the plugin host, because plugin handlers live in
    /// each plugin's own child container rather than the host one.
    /// </summary>
    public interface IPluginEventDispatcher
    {
        Task DispatchAsync<TEvent>(TEvent message, CancellationToken cancellationToken = default) where TEvent : notnull;
    }
}
