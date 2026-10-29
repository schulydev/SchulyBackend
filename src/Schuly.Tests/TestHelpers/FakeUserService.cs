using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakeUserService(params Guid[] schoolUserIds) : IUserService
    {
        public Guid CurrentUserId { get; init; } = Guid.Empty;

        public Task<IReadOnlyList<Guid>> GetCurrentUserSchoolUserIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(schoolUserIds);

        public Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentUserId);

        public Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task TouchLastSeenAsync(string externalId, string? verifiedEmail, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SyncCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
