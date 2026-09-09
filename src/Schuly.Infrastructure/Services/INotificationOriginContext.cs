namespace Schuly.Infrastructure.Services
{
    public interface INotificationOriginContext
    {
        bool IsUserInitiated { get; set; }
    }

    public sealed class NotificationOriginContext : INotificationOriginContext
    {
        public bool IsUserInitiated { get; set; }
    }
}
