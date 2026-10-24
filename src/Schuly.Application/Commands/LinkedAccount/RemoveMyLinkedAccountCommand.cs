using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure.Services;
using Schuly.Plugin.Abstractions;

namespace Schuly.Application.Commands.LinkedAccount
{
    /// <summary>Unlinks one of the signed-in user's accounts and deletes its stored secrets.</summary>
    [AllowAuthenticated]
    public record RemoveMyLinkedAccountCommand(Guid LinkedAccountId) : ICommand<Result>;

    public class RemoveMyLinkedAccountCommandHandler(ILinkedAccounts linkedAccounts, IUserService userService) : ICommandHandler<RemoveMyLinkedAccountCommand, Result>
    {
        public async ValueTask<Result> Handle(RemoveMyLinkedAccountCommand command, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            return await linkedAccounts.RemoveAsync(userId, command.LinkedAccountId, cancellationToken)
                ? Result.Success()
                : Result.Failure($"Linked account with ID '{command.LinkedAccountId}' not found");
        }
    }
}
