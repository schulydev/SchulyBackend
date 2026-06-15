using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.SchoolSystem
{
    [AuthorizedRoles(Roles.Administrator)]
    public record DeleteSchoolSystemCommand(Guid Id) : ICommand<Result>;

    public class DeleteSchoolSystemCommandHandler(SchulyDbContext dbContext) : ICommandHandler<DeleteSchoolSystemCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteSchoolSystemCommand command, CancellationToken cancellationToken)
        {
            var system = await dbContext.SchoolSystems
                .SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

            if (system == null)
                return Result.Failure($"School system with ID {command.Id} not found");

            dbContext.SchoolSystems.Remove(system);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
