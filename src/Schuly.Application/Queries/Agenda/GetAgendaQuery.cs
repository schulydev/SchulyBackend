using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Agenda
{
    [AllowAuthenticated]
    public record GetAgendaQuery(Guid AgendaEntryId) : IQuery<Result<AgendaEntryDto>>;

    public class GetAgendaQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetAgendaQuery, Result<AgendaEntryDto>>
    {
        public async ValueTask<Result<AgendaEntryDto>> Handle(GetAgendaQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.AgendaEntries.AsNoTracking();

            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                dbQuery = dbQuery.Where(AgendaVisibility.For(myIds));
            }

            var agendaEntry = await dbQuery
                .SingleOrDefaultAsync(a => a.Id == query.AgendaEntryId, cancellationToken);

            if (agendaEntry == null)
                return Result<AgendaEntryDto>.Failure($"Agenda entry with ID '{query.AgendaEntryId}' not found");

            return Result<AgendaEntryDto>.Success(agendaEntry.ToDto());
        }
    }
}
