namespace Schuly.API.Services.Notifications
{
    // Used when Firebase isn't configured, so the dispatcher can still drain the outbox.
    public sealed class NoOpPushSender : IPushSender
    {
        public bool IsConfigured => false;

        public Task<PushResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PushResult(0, 0, []));
    }
}
