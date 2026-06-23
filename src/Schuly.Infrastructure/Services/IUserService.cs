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

        /// <summary>
        /// Whether the current user may manage (write to) a class: administrators
        /// always; a teacher linked to a <see cref="Schuly.Domain.Teacher"/> record
        /// only for classes they teach. A teacher with no linked record falls back
        /// to allowed — the role gate has already restricted callers to teachers,
        /// and existing teacher rows aren't linked yet.
        /// </summary>
        Task<bool> CanManageClassAsync(Guid classId, CancellationToken cancellationToken = default);

        bool IsCurrentUserAdmin();
        bool IsCurrentUserTeacher();
    }
}
