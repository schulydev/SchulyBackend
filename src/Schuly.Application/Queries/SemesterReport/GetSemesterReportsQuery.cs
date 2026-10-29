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
    public record GetSemesterReportsQuery() : IQuery<Result<List<SemesterReportDto>>>;

    public class GetSemesterReportsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetSemesterReportsQuery, Result<List<SemesterReportDto>>>
    {
        public async ValueTask<Result<List<SemesterReportDto>>> Handle(GetSemesterReportsQuery query, CancellationToken cancellationToken)
        {
            var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var dbQuery = dbContext.SemesterReports
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.Subjects)
                .Include(r => r.SchoolUser)
                .Where(r => r.SchoolUser!.ApplicationUserId == currentUserId);

            var reports = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<SemesterReportDto>>.Success(reports.ToDto());
        }
    }
}
