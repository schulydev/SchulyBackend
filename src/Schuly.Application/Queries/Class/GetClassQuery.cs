using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Class
{
    [AllowAuthenticated]
    public record GetClassQuery(Guid ClassId) : IQuery<Result<ClassDto>>;

    public class GetClassQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetClassQuery, Result<ClassDto>>
    {
        public async ValueTask<Result<ClassDto>> Handle(GetClassQuery query, CancellationToken cancellationToken)
        {
            var baseQuery = dbContext.Classes
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => c.Id == query.ClassId);

            Domain.Class? classEntity;
            if (userService.IsCurrentUserAdmin())
            {
                classEntity = await baseQuery
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Absences)
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Grades)
                    .Include(c => c.Agenda)
                    .Include(c => c.Exams)
                        .ThenInclude(e => e.Grades)
                    .SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                // Students only see a class they're enrolled in, and only their
                // own grades/absences within the roster — never a classmate's.
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                classEntity = await baseQuery
                    .Where(c => c.Students.Any(su => myIds.Contains(su.Id)))
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Absences.Where(a => myIds.Contains(a.SchoolUserId)))
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Grades.Where(g => myIds.Contains(g.SchoolUserId)))
                    .Include(c => c.Agenda)
                    .Include(c => c.Exams)
                        .ThenInclude(e => e.Grades.Where(g => myIds.Contains(g.SchoolUserId)))
                    .SingleOrDefaultAsync(cancellationToken);
            }

            if (classEntity == null)
                return Result<ClassDto>.Failure($"Class with ID '{query.ClassId}' not found");

            var dto = classEntity.ToDto();
            foreach (var student in dto.Students)
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<ClassDto>.Success(dto);
        }
    }
}
