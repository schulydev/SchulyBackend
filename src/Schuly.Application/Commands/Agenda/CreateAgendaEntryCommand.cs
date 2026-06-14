using Mediator;
using Schuly.Application.Models;
using Schuly.Domain;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
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

    public class CreateAgendaEntryCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<CreateAgendaEntryCommand, Result>
    {
        public async ValueTask<Result> Handle(CreateAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var scopes = (command.ClassId.HasValue ? 1 : 0)
                       + (command.SchoolId.HasValue ? 1 : 0)
                       + (command.SchoolUserId.HasValue ? 1 : 0);
            if (scopes != 1)
                return Result.Failure("Exactly one of ClassId / SchoolId / SchoolUserId must be set");

            // Non-admins may only create their own personal entry. Class- and
            // school-wide entries are admin-only.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (command.SchoolUserId is not Guid ownerId || !myIds.Contains(ownerId))
                    return Result.Forbidden();
            }

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
