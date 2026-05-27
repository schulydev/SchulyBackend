using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;

using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Agenda
{
    [AllowAuthenticated]
    public record GetAgendasQuery() : IQuery<Result<List<AgendaEntryDto>>>;

    public class GetAgendasQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetAgendasQuery, Result<List<AgendaEntryDto>>>
    {
        public async ValueTask<Result<List<AgendaEntryDto>>> Handle(GetAgendasQuery query, CancellationToken cancellationToken)
        {
            var agendaEntries = await dbContext.AgendaEntries.AsNoTracking().ToListAsync(cancellationToken);
            return Result<List<AgendaEntryDto>>.Success(agendaEntries.ToDto());
        }
    }
}
