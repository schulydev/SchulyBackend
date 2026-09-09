using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Notification
{
    [AllowAuthenticated]
    public record RemoveDeviceTokenCommand(string Token) : ICommand<Result>;

    public class RemoveDeviceTokenCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<RemoveDeviceTokenCommand, Result>
    {
        public async ValueTask<Result> Handle(RemoveDeviceTokenCommand command, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var token = await dbContext.DeviceTokens.SingleOrDefaultAsync(t => t.Token == command.Token && t.ApplicationUserId == userId, cancellationToken);

            if (token is not null)
            {
                dbContext.DeviceTokens.Remove(token);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
    }
}
