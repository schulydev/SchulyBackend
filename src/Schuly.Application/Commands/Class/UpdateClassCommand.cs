using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Class
{
    [AuthorizedRoles(Roles.Teacher)]
    public record UpdateClassCommand(Guid ClassId, string Name, string? Description) : ICommand<Result>;

    public class UpdateClassCommandHandler(SchulyDbContext dbContext) : ICommandHandler<UpdateClassCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateClassCommand command, CancellationToken cancellationToken)
        {
            var classEntity = await dbContext.Classes
                .SingleOrDefaultAsync(c => c.Id == command.ClassId, cancellationToken);

            if (classEntity == null)
                return Result.Failure($"Class with ID '{command.ClassId}' not found");

            classEntity.Name = command.Name;
            classEntity.Description = command.Description;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
