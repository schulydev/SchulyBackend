using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;

namespace Schuly.Application.Queries.Teacher
{
    [AllowAuthenticated]
    public record GetTeachersQuery() : IQuery<Result<List<TeacherDto>>>;

    public class GetTeachersQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetTeachersQuery, Result<List<TeacherDto>>>
    {
        public async ValueTask<Result<List<TeacherDto>>> Handle(GetTeachersQuery query, CancellationToken cancellationToken)
        {
            var teachers = await dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.School)
                .ToListAsync(cancellationToken);

            return Result<List<TeacherDto>>.Success(teachers.ToDto());
        }
    }
}
