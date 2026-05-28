using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;

using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Exam
{
    [AllowAuthenticated]
    public record GetExamsQuery() : IQuery<Result<List<ExamDto>>>;

    public class GetExamsQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetExamsQuery, Result<List<ExamDto>>>
    {
        public async ValueTask<Result<List<ExamDto>>> Handle(GetExamsQuery query, CancellationToken cancellationToken)
        {
            var exams = await dbContext.Exams
                .AsNoTracking()
                .Include(e => e.Class)
                .Include(e => e.Grades)
                .ToListAsync(cancellationToken);

            return Result<List<ExamDto>>.Success(exams.ToDto());
        }
    }
}
