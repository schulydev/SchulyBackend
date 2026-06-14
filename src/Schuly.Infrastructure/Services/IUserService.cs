namespace Schuly.Infrastructure.Services
{
    public interface IUserService
    {
        Task SyncCurrentUserAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default);
        Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// The <see cref="Schuly.Domain.SchoolUser"/> ids owned by the current
        /// user (a user can have one per school). Used to scope per-user data —
        /// a row is "owned" when its <c>SchoolUserId</c> is in this set.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetCurrentUserSchoolUserIdsAsync(CancellationToken cancellationToken = default);

        bool IsCurrentUserAdmin();
    }
}
