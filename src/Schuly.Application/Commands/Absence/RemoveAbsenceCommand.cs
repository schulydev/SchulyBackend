using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Absence
{
    [AllowAuthenticated]
    public record RemoveAbsenceCommand(Guid AbsenceId) : ICommand<Result>;

    public class RemoveAbsenceCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<RemoveAbsenceCommand, Result>
    {
        public async ValueTask<Result> Handle(RemoveAbsenceCommand command, CancellationToken cancellationToken)
        {
            var absence = await dbContext.Absences
                .SingleOrDefaultAsync(a => a.Id == command.AbsenceId, cancellationToken);

            if (absence == null)
                return Result.Failure($"Absence with ID '{command.AbsenceId}' not found");

            // Non-admins may only delete their own absences.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (!myIds.Contains(absence.SchoolUserId))
                    return Result.Forbidden();
            }

            dbContext.Absences.Remove(absence);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
