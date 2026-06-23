using Schuly.Plugin.Abstractions;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakePluginUserContext : IPluginUserContext
    {
        public Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.Empty);

        public Task<Guid?> GetCurrentSchoolUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }
}
