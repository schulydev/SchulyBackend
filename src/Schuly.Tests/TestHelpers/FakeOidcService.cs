using Schuly.Infrastructure.Dtos;
using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    /// <summary>Returns a fixed external id so <see cref="UserService"/> can resolve the current user.</summary>
    public sealed class FakeOidcService(string externalId) : IOidcService
    {
        public Task<OidcUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<OidcUser?>(new OidcUser(externalId, null, null, null, []));
    }
}
