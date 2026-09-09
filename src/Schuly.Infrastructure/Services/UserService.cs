using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Schuly.Domain;

namespace Schuly.Infrastructure.Services
{
    public class UserService(IOidcService oidcService, SchulyDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IUserService
    {
        private Guid? _cachedCurrentUserId;

        public bool IsCurrentUserAdmin() =>
            httpContextAccessor.HttpContext?.User.IsAdministrator() ?? false;

        public bool IsCurrentUserTeacher() =>
            httpContextAccessor.HttpContext?.User.IsTeacher() ?? false;

        public async Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
        {
            return await dbContext.ApplicationUsers.AnyAsync(u => u.ExternalId == externalId, cancellationToken);
        }

        public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedCurrentUserId is { } id)
                return id;

            var externalId = oidcService.GetCurrentExternalId();
            if (string.IsNullOrEmpty(externalId))
                throw new UnauthorizedAccessException("No authenticated user");

            var user = await dbContext.ApplicationUsers
                .SingleOrDefaultAsync(u => u.ExternalId == externalId, cancellationToken)
                ?? throw new UnauthorizedAccessException("User not found");

            _cachedCurrentUserId = user.Id;
            return user.Id;
        }

        public async Task<IReadOnlyList<Guid>> GetCurrentUserSchoolUserIdsAsync(CancellationToken cancellationToken = default)
        {
            var currentUserId = await GetCurrentUserIdAsync(cancellationToken);

            return await dbContext.SchoolUsers
                .Where(su => su.ApplicationUserId == currentUserId)
                .Select(su => su.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> CanManageClassAsync(Guid classId, CancellationToken cancellationToken = default)
        {
            if (IsCurrentUserAdmin())
                return true;

            var currentUserId = await GetCurrentUserIdAsync(cancellationToken);

            var teacherIds = await dbContext.Teachers
                .Where(t => t.ApplicationUserId == currentUserId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            if (teacherIds.Count == 0)
                return false;

            return await dbContext.Classes
                .AnyAsync(c => c.Id == classId && c.Teachers.Any(t => teacherIds.Contains(t.Id)), cancellationToken);
        }

        public async Task SyncCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var oidcUser = await oidcService.GetCurrentUserAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("No authenticated user");

            var user = await dbContext.ApplicationUsers
                .SingleOrDefaultAsync(u => u.ExternalId == oidcUser.ExternalId, cancellationToken);

            if (user is null)
            {
                var created = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    ExternalId = oidcUser.ExternalId,
                    Email = !string.IsNullOrWhiteSpace(oidcUser.Email) ? oidcUser.Email : string.Empty,
                    DisplayName = !string.IsNullOrWhiteSpace(oidcUser.DisplayName) ? oidcUser.DisplayName : "Schuly User",
                    ProfilePictureUrl = !string.IsNullOrWhiteSpace(oidcUser.AvatarUrl) ? oidcUser.AvatarUrl : null
                };

                dbContext.ApplicationUsers.Add(created);

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateException)
                {
                    // Another request won the race to create this user - drop our failed
                    // insert and fall through to update the row it created.
                    dbContext.Entry(created).State = EntityState.Detached;
                    user = await dbContext.ApplicationUsers
                        .SingleOrDefaultAsync(u => u.ExternalId == oidcUser.ExternalId, cancellationToken);

                    if (user is null)
                        throw;
                }
            }

            if (!string.IsNullOrWhiteSpace(oidcUser.Email))
                user.Email = oidcUser.Email;

            if (!string.IsNullOrWhiteSpace(oidcUser.DisplayName))
                user.DisplayName = oidcUser.DisplayName;

            if (!string.IsNullOrWhiteSpace(oidcUser.AvatarUrl))
                user.ProfilePictureUrl = oidcUser.AvatarUrl;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
