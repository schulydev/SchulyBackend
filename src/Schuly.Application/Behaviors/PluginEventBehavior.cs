using Mediator;
using Microsoft.Extensions.Logging;
using Schuly.Application.Abstractions;

namespace Schuly.Application.Behaviors
{
    public class PluginEventBehavior<TRequest, TResponse>(IPluginEventDispatcher dispatcher, ILogger<PluginEventBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IMessage
    {
        public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var response = await next(request, cancellationToken);

            if (IsSuccessResult(response))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await dispatcher.DispatchAsync(request, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Plugin event dispatch failed for {Command}", typeof(TRequest).Name);
                    }
                }, CancellationToken.None);
            }

            return response;
        }

        private static bool IsSuccessResult(TResponse response)
        {
            if (response is Models.Result result)
                return result.IsSuccess;

            var type = typeof(TResponse);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Models.Result<>))
            {
                var prop = type.GetProperty("IsSuccess");
                return prop?.GetValue(response) is true;
            }

            return false;
        }
    }
}
