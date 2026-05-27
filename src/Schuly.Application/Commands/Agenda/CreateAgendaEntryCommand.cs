using Mediator;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

using Schuly.Application.Authorization;

namespace Schuly.Application.Commands.Agenda
{
    [AllowAuthenticated]
    public record CreateAgendaEntryCommand(
        AgendaEntryType EntryType,
        string Title,
        string? Description,
        string? Place,
        DateTime Date,
        Guid? ClassId,
        Guid? SchoolId,
        Guid? SchoolUserId) : ICommand<Result>;

    public class CreateAgendaEntryCommandHandler(SchulyDbContext dbContext) : ICommandHandler<CreateAgendaEntryCommand, Result>
    {
        public async ValueTask<Result> Handle(CreateAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var scopes = (command.ClassId.HasValue ? 1 : 0)
                       + (command.SchoolId.HasValue ? 1 : 0)
                       + (command.SchoolUserId.HasValue ? 1 : 0);
            if (scopes != 1)
                return Result.Failure("Exactly one of ClassId / SchoolId / SchoolUserId must be set");

            await dbContext.AgendaEntries.AddAsync(new AgendaEntry
            {
                EntryType = command.EntryType,
                Title = command.Title,
                Description = command.Description,
                Place = command.Place,
                Date = command.Date,
                ClassId = command.ClassId,
                SchoolId = command.SchoolId,
                SchoolUserId = command.SchoolUserId,
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
