using Schuly.Infrastructure.Dtos;
using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakeOidcService(string externalId) : IOidcService
    {
        public Task<OidcUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<OidcUser?>(new OidcUser(externalId, null, null, null, []));
    }
}
