using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Schuly.Plugin.Abstractions.Linking;

namespace Schuly.API.Linking
{
    /// <summary>One in-flight link flow. Holds the current step and the encrypted plugin state, never submitted values.</summary>
    public sealed class LinkSession(string id, Guid userId, string providerId, string flowId, LinkPlatform platform, string? reauthAccountId, string hostCallbackUrl, DateTimeOffset createdAt)
    {
        public string Id { get; } = id;
        public Guid UserId { get; } = userId;
        public string ProviderId { get; } = providerId;
        public string FlowId { get; } = flowId;
        public LinkPlatform Platform { get; } = platform;
        public string? ReauthAccountId { get; } = reauthAccountId;
        public string HostCallbackUrl { get; } = hostCallbackUrl;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public DateTimeOffset AbsoluteExpiry { get; } = createdAt + LinkSessionStore.Ttl;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public LinkStep Step { get; set; } = new() { Id = LinkStepResult.ErrorStepId, Kind = LinkStepKind.Error };
        public string? ProtectedState { get; set; }
        public DateTimeOffset LastPollAt { get; set; } = createdAt;
        public LinkSessionStatus Status { get; set; } = LinkSessionStatus.Active;
        public DateTimeOffset? RetainUntil { get; set; }

        public DateTimeOffset EffectiveExpiry => Step.Kind switch
        {
            LinkStepKind.PushCode when Step.PushCode is { } p => Min(AbsoluteExpiry, p.ExpiresAt + LinkSessionStore.StepGrace),
            LinkStepKind.DeviceCode when Step.DeviceCode is { } d => Min(AbsoluteExpiry, d.ExpiresAt + LinkSessionStore.StepGrace),
            _ => AbsoluteExpiry,
        };

        private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    }

    /// <summary>In-memory session store keyed by an unguessable 128-bit id and bound to the owning user.</summary>
    public sealed class LinkSessionStore(IDataProtectionProvider protection, TimeProvider time)
    {
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan StepGrace = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan TerminalRetention = TimeSpan.FromSeconds(60);
        public const int MaxSessionsPerUser = 10;

        private readonly ConcurrentDictionary<string, LinkSession> _sessions = new(StringComparer.Ordinal);
        private readonly IDataProtector _protector = protection.CreateProtector("Schuly.Linking.SessionState");

        public int Count => _sessions.Count;

        public static string NewId() => Base64Url(RandomNumberGenerator.GetBytes(16));

        public LinkSession Create(string id, Guid userId, string providerId, string flowId, LinkPlatform platform, string? reauthAccountId, string hostCallbackUrl)
        {
            var session = new LinkSession(id, userId, providerId, flowId, platform, reauthAccountId, hostCallbackUrl, time.GetUtcNow());
            var mine = _sessions.Values.Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt).ToList();
            foreach (var old in mine.Take(Math.Max(0, mine.Count - MaxSessionsPerUser + 1)))
                Remove(old.Id);

            _sessions[id] = session;
            return session;
        }

        public LinkSession? Get(string id, Guid userId) =>
            _sessions.TryGetValue(id, out var s) && s.UserId == userId ? s : null;

        /// <summary>The callback route is anonymous: the unguessable session id is the capability.</summary>
        public LinkSession? GetByCapability(string id) => _sessions.TryGetValue(id, out var s) ? s : null;

        public bool IsExpired(LinkSession s) => time.GetUtcNow() >= (s.RetainUntil ?? s.EffectiveExpiry);

        public void Remove(string id) => _sessions.TryRemove(id, out _);

        public string? Protect(string? state) => state is null ? null : _protector.Protect(state);

        public string? Unprotect(string? protectedState)
        {
            if (protectedState is null)
                return null;
            try
            {
                return _protector.Unprotect(protectedState);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        public int Sweep()
        {
            var removed = 0;
            foreach (var s in _sessions.Values)
            {
                if (IsExpired(s) && _sessions.TryRemove(s.Id, out _))
                    removed++;
            }
            return removed;
        }

        /// <summary>Test hook: every state string currently held, as stored.</summary>
        public IReadOnlyList<LinkSession> Snapshot() => _sessions.Values.ToList();

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public sealed class LinkSessionSweeper(LinkSessionStore store) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(stoppingToken))
                store.Sweep();
        }
    }
}
