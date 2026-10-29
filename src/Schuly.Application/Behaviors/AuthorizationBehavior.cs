using Mediator;
using Schuly.Application.Authorization;
using System.Reflection;

namespace Schuly.Application.Behaviors
{
    public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IMessage
    {
        public ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var type = request.GetType();

            if (type.GetCustomAttribute<AllowAuthenticatedAttribute>() is null)
                throw new InvalidOperationException(
                    $"Missing authorization attribute on {type.Name}. Add [AllowAuthenticated].");

            return next(request, cancellationToken);
        }
    }
}
