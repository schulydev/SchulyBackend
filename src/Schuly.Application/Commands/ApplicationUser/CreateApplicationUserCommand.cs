using Mediator;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.ApplicationUser
{
    [AuthorizedRoles(Roles.Administrator)]
    public record CreateApplicationUserCommand(string ExternalId, string Email, string? DisplayName) : ICommand<Result<Guid>>;

    public class CreateApplicationUserCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateApplicationUserCommand, Result<Guid>>
    {
        public async ValueTask<Result<Guid>> Handle(CreateApplicationUserCommand command, CancellationToken cancellationToken)
        {
            var applicationUser = new Domain.ApplicationUser
            {
                Id = Guid.NewGuid(),
                ExternalId = command.ExternalId,
                Email = command.Email,
                DisplayName = command.DisplayName ?? "Schuly User"
            };

            await dbContext.ApplicationUsers.AddAsync(applicationUser, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(applicationUser.Id);
        }
    }
}
