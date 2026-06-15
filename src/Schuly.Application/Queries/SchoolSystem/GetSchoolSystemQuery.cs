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
    public record GetSchoolSystemQuery(Guid Id) : IQuery<Result<SchoolSystemDto>>;

    public class GetSchoolSystemQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetSchoolSystemQuery, Result<SchoolSystemDto>>
    {
        public async ValueTask<Result<SchoolSystemDto>> Handle(GetSchoolSystemQuery query, CancellationToken cancellationToken)
        {
            var system = await dbContext.SchoolSystems
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == query.Id, cancellationToken);

            if (system == null)
                return Result<SchoolSystemDto>.Failure($"School system with ID '{query.Id}' not found");

            return Result<SchoolSystemDto>.Success(system.ToDto());
        }
    }
}
