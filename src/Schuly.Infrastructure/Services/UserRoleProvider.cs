using Microsoft.Extensions.Configuration;
using Schuly.Domain.Enums;
using Toamaisutaa.Abstractions;

namespace Schuly.Infrastructure.Services
{
    public class UserRoleProvider(IConfiguration configuration) : IUserRoleProvider
    {
        public Task<IReadOnlyList<string>> GetRolesAsync(ToamaisutaaUser user, CancellationToken cancellationToken = default)
        {
            var administrators = configuration.GetSection("Auth:Administrators").Get<string[]>() ?? [];
            var isAdministrator = administrators.Any(entry =>
                string.Equals(entry, user.Email, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry, user.UserName, StringComparison.OrdinalIgnoreCase));

            IReadOnlyList<string> roles = isAdministrator ? [nameof(Roles.Administrator)] : [];
            return Task.FromResult(roles);
        }
    }
}
