using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Absence
{
    [AllowAuthenticated]
    public record UpdateAbsenceCommand(Guid AbsenceId, string Reason, AbsenceType Type, DateTime From, DateTime Until, Guid SchoolUserId) : ICommand<Result>;

    public class UpdateAbsenceCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<UpdateAbsenceCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateAbsenceCommand command, CancellationToken cancellationToken)
        {
            var absence = await dbContext.Absences
                .SingleOrDefaultAsync(a => a.Id == command.AbsenceId, cancellationToken);

            if (absence == null)
                return Result.Failure($"Absence with ID '{command.AbsenceId}' not found");

            // Non-admins may only touch their own absences, and may not reassign
            // one to a SchoolUser they don't own.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (!myIds.Contains(absence.SchoolUserId) || !myIds.Contains(command.SchoolUserId))
                    return Result.Failure("Forbidden");
            }

            absence.Reason = command.Reason;
            absence.Type = command.Type;
            absence.From = command.From;
            absence.Until = command.Until;
            absence.SchoolUserId = command.SchoolUserId;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
