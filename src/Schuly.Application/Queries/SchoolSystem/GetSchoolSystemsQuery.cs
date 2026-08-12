using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;

namespace Schuly.Application.Queries.SchoolSystem
{
    [AllowAuthenticated]
    public record GetSchoolSystemsQuery(bool IncludeDisabled = false) : IQuery<Result<List<SchoolSystemDto>>>;

    public class GetSchoolSystemsQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetSchoolSystemsQuery, Result<List<SchoolSystemDto>>>
    {
        public async ValueTask<Result<List<SchoolSystemDto>>> Handle(GetSchoolSystemsQuery query, CancellationToken cancellationToken)
        {
            var systems = await dbContext.SchoolSystems
                .AsNoTracking()
                .Where(s => query.IncludeDisabled || s.Enabled)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.DisplayName)
                .ToListAsync(cancellationToken);

            return Result<List<SchoolSystemDto>>.Success(systems.ToDto());
        }
    }
}
