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
    public record GetAgendasQuery() : IQuery<Result<List<AgendaEntryDto>>>;

    public class GetAgendasQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetAgendasQuery, Result<List<AgendaEntryDto>>>
    {
        public async ValueTask<Result<List<AgendaEntryDto>>> Handle(GetAgendasQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.AgendaEntries.AsNoTracking();

            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                dbQuery = dbQuery.Where(AgendaVisibility.For(myIds));
            }

            var agendaEntries = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<AgendaEntryDto>>.Success(agendaEntries.ToDto());
        }
    }
}
