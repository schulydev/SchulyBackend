using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    /// <summary>
    /// Test double for <see cref="IUserService"/> — lets a test pretend to be a
    /// specific user (by their SchoolUser ids) or an administrator, without an
    /// OIDC round-trip.
    /// </summary>
    public sealed class FakeUserService(bool isAdmin, params Guid[] schoolUserIds) : IUserService
    {
        /// <summary>Decides <see cref="CanManageClassAsync"/>; allows everything by default.</summary>
        public Func<Guid, bool> ClassManager { get; init; } = _ => true;

        public bool IsCurrentUserAdmin() => isAdmin;

        public Task<IReadOnlyList<Guid>> GetCurrentUserSchoolUserIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(schoolUserIds);

        public Task<bool> CanManageClassAsync(Guid classId, CancellationToken cancellationToken = default)
            => Task.FromResult(isAdmin || ClassManager(classId));

        public Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.Empty);

        public Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task SyncCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
