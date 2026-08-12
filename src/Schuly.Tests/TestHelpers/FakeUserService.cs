using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakeUserService(bool isAdmin, params Guid[] schoolUserIds) : IUserService
    {
        public Func<Guid, bool> ClassManager { get; init; } = _ => true;

        public bool IsCurrentUserAdmin() => isAdmin;

        public bool IsCurrentUserTeacher() => false;

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
