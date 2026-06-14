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
    public record EnrolStudentCommand(Guid UserId, Guid ClassId) : ICommand<Result>;

    public class EnrolStudentCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<EnrolStudentCommand, Result>
    {
        public async ValueTask<Result> Handle(EnrolStudentCommand command, CancellationToken cancellationToken)
        {
            var schoolUser = await dbContext.SchoolUsers.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
            if (schoolUser == null)
                return Result.Failure($"SchoolUser with ID '{command.UserId}' not found");

            var @class = await dbContext.Classes.AsTracking().SingleOrDefaultAsync(c => c.Id == command.ClassId, cancellationToken);
            if (@class == null)
                return Result.Failure($"Class with ID '{command.ClassId}' not found");

            if (!await userService.CanManageClassAsync(command.ClassId, cancellationToken))
                return Result.Forbidden();

            @class.Students.Add(schoolUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
