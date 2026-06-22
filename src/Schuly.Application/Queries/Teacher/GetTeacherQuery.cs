using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.Teacher
{
    [AllowAuthenticated]
    public record GetTeacherQuery(Guid TeacherId) : IQuery<Result<TeacherDto>>;

    public class GetTeacherQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetTeacherQuery, Result<TeacherDto>>
    {
        public async ValueTask<Result<TeacherDto>> Handle(GetTeacherQuery query, CancellationToken cancellationToken)
        {
            // Scope to the caller's own teachers; admins may read any. A miss
            // returns "not found" rather than leaking that the id exists.
            var isAdmin = userService.IsCurrentUserAdmin();
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var teacher = await dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.School)
                .SingleOrDefaultAsync(
                    t => t.Id == query.TeacherId && (isAdmin || t.ApplicationUserId == userId),
                    cancellationToken);

            if (teacher == null)
                return Result<TeacherDto>.Failure($"Teacher with ID '{query.TeacherId}' not found");

            return Result<TeacherDto>.Success(teacher.ToDto());
        }
    }
}
