using Schuly.Infrastructure.Services;

namespace Schuly.Tests.TestHelpers
{
    public sealed class FakeNotificationOriginContext(bool isUserInitiated = false) : INotificationOriginContext
    {
        public bool IsUserInitiated { get; set; } = isUserInitiated;
    }
}
