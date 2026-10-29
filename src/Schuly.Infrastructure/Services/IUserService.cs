namespace Schuly.Infrastructure.Services
{
    public interface IUserService
    {
        Task SyncCurrentUserAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stamps <see cref="Schuly.Domain.ApplicationUser.LastSeenAt"/> to now, but at
        /// most once an hour, so a busy client does not write on every request.
        /// </summary>
        Task TouchLastSeenAsync(string externalId, string? verifiedEmail, CancellationToken cancellationToken = default);
        Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Guid>> GetCurrentUserSchoolUserIdsAsync(CancellationToken cancellationToken = default);
    }
}
