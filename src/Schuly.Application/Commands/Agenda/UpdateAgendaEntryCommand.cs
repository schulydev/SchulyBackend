using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Agenda
{
    [AllowAuthenticated]
    public record UpdateAgendaEntryCommand(
        Guid AgendaEntryId,
        AgendaEntryType EntryType,
        string Title,
        string? Description,
        string? Place,
        DateTime Date,
        Guid? ClassId,
        Guid? SchoolId,
        Guid? SchoolUserId) : ICommand<Result>;

    public class UpdateAgendaEntryCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<UpdateAgendaEntryCommand, Result>
    {
        public async ValueTask<Result> Handle(UpdateAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var scopes = (command.ClassId.HasValue ? 1 : 0)
                       + (command.SchoolId.HasValue ? 1 : 0)
                       + (command.SchoolUserId.HasValue ? 1 : 0);
            if (scopes != 1)
                return Result.Failure("Exactly one of ClassId / SchoolId / SchoolUserId must be set");

            var agendaEntry = await dbContext.AgendaEntries
                .SingleOrDefaultAsync(a => a.Id == command.AgendaEntryId, cancellationToken);

            if (agendaEntry == null)
                return Result.Failure($"Agenda entry with ID '{command.AgendaEntryId}' not found");

            // Non-admins may only edit their own personal entry, and may not turn
            // it into a class/school entry or hand it to another user.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                var ownsExisting = agendaEntry.SchoolUserId is Guid existing && myIds.Contains(existing);
                var ownsTarget = command.SchoolUserId is Guid target && myIds.Contains(target);
                if (!ownsExisting || !ownsTarget)
                    return Result.Failure("Forbidden");
            }

            agendaEntry.EntryType = command.EntryType;
            agendaEntry.Title = command.Title;
            agendaEntry.Description = command.Description;
            agendaEntry.Place = command.Place;
            agendaEntry.Date = command.Date;
            agendaEntry.ClassId = command.ClassId;
            agendaEntry.SchoolId = command.SchoolId;
            agendaEntry.SchoolUserId = command.SchoolUserId;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
