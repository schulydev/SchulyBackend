using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Agenda
{
    [AllowAuthenticated]
    public record DeleteAgendaEntryCommand(Guid AgendaEntryId) : ICommand<Result>;

    public class DeleteAgendaEntryCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<DeleteAgendaEntryCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var agendaEntry = await dbContext.AgendaEntries
                .SingleOrDefaultAsync(a => a.Id == command.AgendaEntryId, cancellationToken);

            if (agendaEntry == null)
                return Result.Failure($"Agenda entry with ID '{command.AgendaEntryId}' not found");

            // Non-admins may only delete their own personal entry.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (agendaEntry.SchoolUserId is not Guid ownerId || !myIds.Contains(ownerId))
                    return Result.Forbidden();
            }

            dbContext.AgendaEntries.Remove(agendaEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
