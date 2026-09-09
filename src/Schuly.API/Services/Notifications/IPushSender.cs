namespace Schuly.API.Services.Notifications
{
    public sealed record PushMessage(string Title, string? Body, IReadOnlyDictionary<string, string> Data);
    public sealed record PushResult(int SuccessCount, int FailureCount, IReadOnlyList<string> UnregisteredTokens);

    public interface IPushSender
    {
        bool IsConfigured { get; }
        Task<PushResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken = default);
    }
}
