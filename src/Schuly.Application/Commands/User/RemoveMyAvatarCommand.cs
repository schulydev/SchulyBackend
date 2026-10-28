using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;

namespace Schuly.Application.Commands.User
{
    [AllowAuthenticated]
    public record RemoveMyAvatarCommand() : ICommand<Result>;

    public class RemoveMyAvatarCommandHandler(SchulyDbContext dbContext, IUserService userService, IDocumentStorage storage) : ICommandHandler<RemoveMyAvatarCommand, Result>
    {
        public async ValueTask<Result> Handle(RemoveMyAvatarCommand command, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var user = await dbContext.ApplicationUsers.FindAsync([userId], cancellationToken);
            if (user is null)
                return Result.Failure("User not found");

            var previous = user.ProfilePictureUrl;
            user.ProfilePictureUrl = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (SetMyAvatarCommandHandler.IsStoredKey(previous))
                await storage.DeleteAsync(previous!, cancellationToken);

            return Result.Success();
        }
    }
}
