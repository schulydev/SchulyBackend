using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Class
{
    [AuthorizedRoles(Roles.Teacher)]
    public record DeleteClassCommand(Guid ClassId) : ICommand<Result>;

    public class DeleteClassCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<DeleteClassCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteClassCommand command, CancellationToken cancellationToken)
        {
            var classEntity = await dbContext.Classes
                .SingleOrDefaultAsync(c => c.Id == command.ClassId, cancellationToken);

            if (classEntity == null)
                return Result.Failure($"Class with ID '{command.ClassId}' not found");

            if (!await userService.CanManageClassAsync(command.ClassId, cancellationToken))
                return Result.Forbidden();

            dbContext.Classes.Remove(classEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
