using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Exam
{
    [AllowAuthenticated]
    public record GetExamsQuery() : IQuery<Result<List<ExamDto>>>;

    public class GetExamsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetExamsQuery, Result<List<ExamDto>>>
    {
        public async ValueTask<Result<List<ExamDto>>> Handle(GetExamsQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.Exams
                .AsNoTracking()
                .Include(e => e.Class)
                .AsQueryable();

            if (userService.IsCurrentUserAdmin())
            {
                dbQuery = dbQuery.Include(e => e.Grades);
            }
            else
            {
                // Students only see exams for classes they're enrolled in, and
                // only their own grade on each — never a classmate's.
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                dbQuery = dbQuery
                    .Where(e => e.Class!.Students.Any(su => myIds.Contains(su.Id)))
                    .Include(e => e.Grades.Where(g => myIds.Contains(g.SchoolUserId)));
            }

            var exams = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<ExamDto>>.Success(exams.ToDto());
        }
    }
}
