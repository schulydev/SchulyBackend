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
    public record GetTeacherQuery(Guid TeacherId) : IQuery<Result<TeacherDto>>;

    public class GetTeacherQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetTeacherQuery, Result<TeacherDto>>
    {
        public async ValueTask<Result<TeacherDto>> Handle(GetTeacherQuery query, CancellationToken cancellationToken)
        {
            var teacher = await dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.School)
                .SingleOrDefaultAsync(t => t.Id == query.TeacherId, cancellationToken);

            if (teacher == null)
                return Result<TeacherDto>.Failure($"Teacher with ID '{query.TeacherId}' not found");

            return Result<TeacherDto>.Success(teacher.ToDto());
        }
    }
}
