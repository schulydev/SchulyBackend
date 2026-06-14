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
    public record GetExamQuery(Guid ExamId) : IQuery<Result<ExamDto>>;

    public class GetExamQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetExamQuery, Result<ExamDto>>
    {
        public async ValueTask<Result<ExamDto>> Handle(GetExamQuery query, CancellationToken cancellationToken)
        {
            // Students only see an exam for a class they're enrolled in, and only
            // their own grade on it. Admins see all.
            var isAdmin = userService.IsCurrentUserAdmin();
            IReadOnlyList<Guid> myIds = isAdmin ? [] : await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            IQueryable<Domain.Exam> dbQuery = dbContext.Exams.AsNoTracking().Include(e => e.Class);

            if (!isAdmin)
                dbQuery = dbQuery.Where(e => e.Class!.Students.Any(su => myIds.Contains(su.Id)));

            var exam = await dbQuery
                .Include(e => e.Grades.Where(g => isAdmin || myIds.Contains(g.SchoolUserId)))
                .SingleOrDefaultAsync(e => e.Id == query.ExamId, cancellationToken);

            if (exam == null)
                return Result<ExamDto>.Failure($"Exam with ID '{query.ExamId}' not found");

            return Result<ExamDto>.Success(exam.ToDto());
        }
    }
}
