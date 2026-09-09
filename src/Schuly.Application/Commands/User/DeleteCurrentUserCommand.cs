using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.User
{
    [AllowAuthenticated]
    public record DeleteCurrentUserCommand() : ICommand<Result>;

    public class DeleteCurrentUserCommandHandler(SchulyDbContext dbContext, IUserService userService, IAccountPurger accountPurger, IIdentityProviderAdmin identityProviderAdmin) : ICommandHandler<DeleteCurrentUserCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteCurrentUserCommand command, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var externalId = await dbContext.ApplicationUsers.Where(u => u.Id == userId).Select(u => u.ExternalId).SingleOrDefaultAsync(cancellationToken);

            if (externalId is null)
                return Result.Failure("User not found");

            await accountPurger.PurgeApplicationUserAsync(userId, cancellationToken);

            // The client logs its own outcome; a failure there must not fail the request,
            // since everything this backend stores is already gone.
            await identityProviderAdmin.DeleteUserAsync(externalId, cancellationToken);

            return Result.Success();
        }
    }
}
