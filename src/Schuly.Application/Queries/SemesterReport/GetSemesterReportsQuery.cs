using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.SemesterReport
{
    [AllowAuthenticated]
    public record GetSemesterReportsQuery(Guid? SchoolUserId = null) : IQuery<Result<List<SemesterReportDto>>>;

    public class GetSemesterReportsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetSemesterReportsQuery, Result<List<SemesterReportDto>>>
    {
        public async ValueTask<Result<List<SemesterReportDto>>> Handle(GetSemesterReportsQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.SemesterReports
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.Subjects)
                .Include(r => r.SchoolUser)
                .AsQueryable();

            if (query.SchoolUserId.HasValue)
                dbQuery = dbQuery.Where(r => r.SchoolUserId == query.SchoolUserId.Value);

            if (!userService.IsCurrentUserAdmin())
            {
                var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);
                dbQuery = dbQuery.Where(r => r.SchoolUser!.ApplicationUserId == currentUserId);
            }

            var reports = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<SemesterReportDto>>.Success(reports.ToDto());
        }
    }
}
