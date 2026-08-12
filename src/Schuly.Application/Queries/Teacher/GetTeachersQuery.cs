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
    public record GetTeachersQuery() : IQuery<Result<List<TeacherDto>>>;

    public class GetTeachersQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetTeachersQuery, Result<List<TeacherDto>>>
    {
        public async ValueTask<Result<List<TeacherDto>>> Handle(GetTeachersQuery query, CancellationToken cancellationToken)
        {
            var isAdmin = userService.IsCurrentUserAdmin();
            IQueryable<Domain.Teacher> dbQuery = dbContext.Teachers.AsNoTracking().Include(t => t.School);

            if (!isAdmin)
            {
                var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
                dbQuery = dbQuery.Where(t => t.ApplicationUserId == userId);
            }

            var teachers = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<TeacherDto>>.Success(teachers.ToDto());
        }
    }
}
