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
            var dbQuery = dbContext.Absences
                .AsNoTracking()
                .Include(a => a.SchoolUser)
                .AsQueryable();

            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                dbQuery = dbQuery.Where(a => myIds.Contains(a.SchoolUserId));
            }

            var absences = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<AbsenceDto>>.Success(absences.ToDto());
        }
    }
}
