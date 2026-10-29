using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Schuly.Domain.Enums;
using Toamaisutaa.Abstractions;

namespace Schuly.Infrastructure.Services
{
    public class UserRoleProvider(IConfiguration configuration, SchulyDbContext db) : IUserRoleProvider
    {
        public async Task<IReadOnlyList<string>> GetRolesAsync(ToamaisutaaUser user, CancellationToken cancellationToken = default)
        {
            var administrators = configuration.GetSection("Auth:Administrators").Get<string[]>() ?? [];
            var isAdministrator = administrators.Any(entry => string.Equals(entry, user.UserName, StringComparison.OrdinalIgnoreCase))
                || (administrators.Any(entry => string.Equals(entry, user.Email, StringComparison.OrdinalIgnoreCase)) && await HasProvenEmailAsync(user, cancellationToken));

            return isAdministrator ? [nameof(Roles.Administrator)] : [];
        }

        /// <summary>
        /// Whether the user's email counts for an administrator match. A password account must have proven it, or anyone
        /// registering the configured address first would become administrator. An account without a password only gets
        /// an email from a sign-in provider that marked it verified (<c>TrustEmailVerified</c>).
        /// </summary>
        private async Task<bool> HasProvenEmailAsync(ToamaisutaaUser user, CancellationToken cancellationToken)
        {
            var credential = await db.Set<ToamaisutaaPasswordCredential>().AsNoTracking().Where(c => c.UserId == user.Id).Select(c => new { c.Email, c.EmailConfirmedAt }).FirstOrDefaultAsync(cancellationToken);
            if (credential is null)
                return true;

            return credential.EmailConfirmedAt is not null && string.Equals(credential.Email, user.Email, StringComparison.OrdinalIgnoreCase);
        }
    }
}
