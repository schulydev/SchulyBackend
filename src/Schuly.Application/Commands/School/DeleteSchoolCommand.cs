using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Commands.School
{
    [AuthorizedRoles(Roles.Administrator)]
    public record DeleteSchoolCommand(Guid Id) : ICommand<Result>;

    public class DeleteSchoolCommandHandler(SchulyDbContext dbContext) : ICommandHandler<DeleteSchoolCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteSchoolCommand command, CancellationToken cancellationToken)
        {
            var school = await dbContext.Schools
                .Include(s => s.SchoolUsers)
                .Include(s => s.Classes)
                .Include(s => s.Teachers)
                .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken: cancellationToken);

            if (school == null)
                return Result.Failure($"School with ID {command.Id} not found");

            // All three relationships are Restrict — deleting with any dependent
            // would raise an FK violation, so block it cleanly first (409).
            if (school.SchoolUsers.Any() || school.Classes.Any() || school.Teachers.Any())
                return Result.Conflict("Cannot delete school that has associated users, classes or teachers");

            dbContext.Schools.Remove(school);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
