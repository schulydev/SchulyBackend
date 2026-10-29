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

            // Entries with an @ are addresses: they only ever match a proven email, never a user name.
            var byUserName = administrators.Any(entry => !entry.Contains('@') && string.Equals(entry, user.UserName, StringComparison.OrdinalIgnoreCase));
            var byEmail = !string.IsNullOrWhiteSpace(user.Email)
                && administrators.Any(entry => entry.Contains('@') && string.Equals(entry, user.Email, StringComparison.OrdinalIgnoreCase))
                && await HasProvenEmailAsync(user.Id, user.Email, cancellationToken);

            return byUserName || byEmail ? [nameof(Roles.Administrator)] : [];
        }

        /// <summary>
        /// Positive proof that the user controls <paramref name="email"/>: the password account confirmed it, or a linked
        /// sign-in provider configured with TrustEmailVerified marked it verified. Anything else fails closed, so registering the configured address first
        /// without proving it never makes someone administrator.
        /// </summary>
        private async Task<bool> HasProvenEmailAsync(Guid userId, string email, CancellationToken cancellationToken)
        {
            var normalized = email.ToUpperInvariant();

            var confirmedByPassword = await db.Set<ToamaisutaaPasswordCredential>().AsNoTracking()
                .AnyAsync(c => c.UserId == userId && c.EmailConfirmedAt != null && c.Email != null && c.Email.ToUpper() == normalized, cancellationToken);
            if (confirmedByPassword)
                return true;

            // Only providers configured with TrustEmailVerified count: others may let people pick their own address.
            var trusted = configuration.GetSection("ExternalLogin:Providers").GetChildren().Where(p => p.GetValue<bool>("TrustEmailVerified")).Select(p => p.Key).ToList();
            if (trusted.Count == 0)
                return false;

            return await db.Set<ToamaisutaaExternalLogin>().AsNoTracking()
                .AnyAsync(l => l.UserId == userId && trusted.Contains(l.ProviderKey) && l.EmailVerified && l.Email != null && l.Email.ToUpper() == normalized, cancellationToken);
        }
    }
}
