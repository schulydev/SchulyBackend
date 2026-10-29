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
            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var absence = await dbContext.Absences
                .SingleOrDefaultAsync(a => a.Id == command.AbsenceId && myIds.Contains(a.SchoolUserId), cancellationToken);

            if (absence == null)
                return Result.NotFound($"Absence with ID '{command.AbsenceId}' not found");

            if (ManualEdit.RefuseSynced(absence) is { } synced)
                return synced;

            dbContext.Absences.Remove(absence);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
