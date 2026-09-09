using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.Notification
{
    [AllowAuthenticated]
    public record RegisterDeviceTokenCommand(string Token, string Platform, string Locale) : ICommand<Result>;

    public class RegisterDeviceTokenCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<RegisterDeviceTokenCommand, Result>
    {
        private static readonly string[] AllowedPlatforms = ["android", "ios", "web"];
        private static readonly string[] AllowedLocales = ["de", "en"];

        public async ValueTask<Result> Handle(RegisterDeviceTokenCommand command, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(command.Token) || command.Token.Length > 500)
                return Result.Failure("Token must be non-empty and at most 500 characters.");

            var platform = command.Platform.ToLowerInvariant();
            if (!AllowedPlatforms.Contains(platform))
                return Result.Failure("Platform must be one of: android, ios, web.");

            var locale = command.Locale.ToLowerInvariant();
            if (!AllowedLocales.Contains(locale))
                return Result.Failure("Locale must be one of: de, en.");

            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var existing = await dbContext.DeviceTokens.SingleOrDefaultAsync(t => t.Token == command.Token, cancellationToken);

            if (existing is not null)
            {
                existing.ApplicationUserId = userId;
                existing.Platform = platform;
                existing.Locale = locale;
                existing.LastSeenAt = DateTime.UtcNow;
            }
            else
            {
                await dbContext.DeviceTokens.AddAsync(new DeviceToken
                {
                    ApplicationUserId = userId,
                    Token = command.Token,
                    Platform = platform,
                    Locale = locale,
                    LastSeenAt = DateTime.UtcNow
                }, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
