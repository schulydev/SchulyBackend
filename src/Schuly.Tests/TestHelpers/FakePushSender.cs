using Schuly.API.Services.Notifications;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakePushSender : IPushSender
    {
        public bool IsConfigured { get; set; } = true;
        public List<(IReadOnlyList<string> Tokens, PushMessage Message)> Sends { get; } = [];
        public IReadOnlyList<string> UnregisteredTokensToReturn { get; set; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task<PushResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            Sends.Add((tokens, message));
            return Task.FromResult(new PushResult(tokens.Count, 0, UnregisteredTokensToReturn));
        }
    }
}
