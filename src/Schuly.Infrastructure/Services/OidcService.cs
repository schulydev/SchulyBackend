using Microsoft.AspNetCore.Http;
using Schuly.Infrastructure.Dtos;
using System.Security.Claims;

namespace Schuly.Infrastructure.Services
{
    public class OidcService(IHttpContextAccessor httpContextAccessor) : IOidcService
    {
        public string? GetCurrentExternalId()
        {
            var identity = httpContextAccessor.HttpContext?.User.Identity as ClaimsIdentity;
            if (identity is null || !identity.IsAuthenticated)
                return null;

            var externalId = identity.FindFirst("sub")?.Value
                ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return string.IsNullOrEmpty(externalId) ? null : externalId;
        }

        public Task<OidcUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (httpContextAccessor.HttpContext?.User.Identity is not ClaimsIdentity identity)
                return Task.FromResult<OidcUser?>(null);

            var externalId = GetCurrentExternalId();
            if (string.IsNullOrEmpty(externalId))
                return Task.FromResult<OidcUser?>(null);

            var user = new OidcUser(
                externalId,
                identity.FindFirst("email")?.Value,
                identity.FindFirst("name")?.Value ?? identity.FindFirst("preferred_username")?.Value,
                identity.FindFirst("picture")?.Value,
                identity.FindAll(identity.RoleClaimType).Select(c => c.Value).ToList());

            return Task.FromResult<OidcUser?>(user);
        }
    }
}
