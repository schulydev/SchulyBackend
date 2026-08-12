using Mediator;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Absence
{
    [AllowAuthenticated]
    public record CreateAbsenceCommand(string Reason, AbsenceType Type, DateTime From, DateTime Until, Guid SchoolUserId) : ICommand<Result>;

    public class CreateAbsenceCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<CreateAbsenceCommand, Result>
    {
        public async ValueTask<Result> Handle(CreateAbsenceCommand command, CancellationToken cancellationToken)
        {
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (!myIds.Contains(command.SchoolUserId))
                    return Result.Forbidden();
            }

            await dbContext.Absences.AddAsync(new Domain.Absence
            {
                Reason = command.Reason,
                Type = command.Type,
                From = command.From,
                Until = command.Until,
                SchoolUserId = command.SchoolUserId
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
