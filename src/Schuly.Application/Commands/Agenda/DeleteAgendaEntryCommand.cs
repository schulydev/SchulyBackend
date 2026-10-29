using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Models;
using Schuly.Application.Queries.Agenda;
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
            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var agendaEntry = await dbContext.AgendaEntries
                .Where(AgendaVisibility.For(myIds))
                .SingleOrDefaultAsync(a => a.Id == command.AgendaEntryId, cancellationToken);

            if (agendaEntry == null)
                return Result.NotFound($"Agenda entry with ID '{command.AgendaEntryId}' not found");

            if (ManualEdit.RefuseSynced(agendaEntry) is { } synced)
                return synced;

            dbContext.AgendaEntries.Remove(agendaEntry);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
