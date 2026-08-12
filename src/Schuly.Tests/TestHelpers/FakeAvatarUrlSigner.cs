using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakeAvatarUrlSigner : IAvatarUrlSigner
    {
        public string? ToPublicUrl(Guid schoolUserId, string? stored) => stored;
        public bool Verify(Guid schoolUserId, long exp, string sig) => true;
    }
}
