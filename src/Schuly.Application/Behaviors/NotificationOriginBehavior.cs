using Mediator;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Behaviors
{
    // Requests reaching this behavior went through the API pipeline, i.e. they are the
    // user's own actions - those must never produce a push notification. Plugin sync
    // writes go through SaveChanges directly, in their own host scope, where the flag
    // stays false and the outbox interceptor is free to act.
    public class NotificationOriginBehavior<TRequest, TResponse>(INotificationOriginContext origin) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IMessage
    {
        public ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            origin.IsUserInitiated = true;
            return next(request, cancellationToken);
        }
    }
}
