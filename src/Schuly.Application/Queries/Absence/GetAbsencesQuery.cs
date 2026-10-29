using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Absence
{
    [AllowAuthenticated]
    public record GetAbsencesQuery() : IQuery<Result<List<AbsenceDto>>>;

    public class GetAbsencesQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetAbsencesQuery, Result<List<AbsenceDto>>>
    {
        public async ValueTask<Result<List<AbsenceDto>>> Handle(GetAbsencesQuery query, CancellationToken cancellationToken)
        {
            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var absences = await dbContext.Absences
                .AsNoTracking()
                .Include(a => a.SchoolUser)
                .Where(a => myIds.Contains(a.SchoolUserId))
                .ToListAsync(cancellationToken);
            return Result<List<AbsenceDto>>.Success(absences.ToDto());
        }
    }
}
