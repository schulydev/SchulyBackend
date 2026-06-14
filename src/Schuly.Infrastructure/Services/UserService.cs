using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Schuly.Domain;
using Schuly.Domain.Enums;
using System.Security.Claims;

namespace Schuly.Infrastructure.Services
{
    public class UserService(
        IOidcService oidcService,
        SchulyDbContext dbContext,
        IHttpContextAccessor httpContextAccessor) : IUserService
    {
        public bool IsCurrentUserAdmin()
        {
            var roleClaim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<Roles>(roleClaim, out var role) && role == Roles.Administrator;
        }


        public async Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
        {
            return await dbContext.ApplicationUsers.AnyAsync(u => u.ExternalId == externalId, cancellationToken);
        }

        public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
        {
            var oidcUser = await oidcService.GetCurrentUserAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("No authenticated user");

            var user = await dbContext.ApplicationUsers
                .SingleOrDefaultAsync(u => u.ExternalId == oidcUser.ExternalId, cancellationToken)
                ?? throw new UnauthorizedAccessException("User not found");

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

            // Unlinked teacher: keep the pre-link, role-only behaviour.
            if (teacherIds.Count == 0)
                return true;

            return await dbContext.Classes
                .AnyAsync(c => c.Id == classId && c.Teachers.Any(t => teacherIds.Contains(t.Id)), cancellationToken);
        }

        public async Task SyncCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var oidcUser = await oidcService.GetCurrentUserAsync(cancellationToken)
                ?? throw new UnauthorizedAccessException("No authenticated user");

            var user = await dbContext.ApplicationUsers
                .SingleOrDefaultAsync(u => u.ExternalId == oidcUser.ExternalId, cancellationToken);

            var email = oidcUser.Email ?? string.Empty;
            var displayName = oidcUser.DisplayName ?? "Schuly User";

            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    ExternalId = oidcUser.ExternalId,
                    Email = email,
                    DisplayName = displayName,
                    ProfilePictureUrl = oidcUser.AvatarUrl
                };

                dbContext.ApplicationUsers.Add(user);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            user.Email = email;
            user.DisplayName = displayName;
            user.ProfilePictureUrl = oidcUser.AvatarUrl;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
