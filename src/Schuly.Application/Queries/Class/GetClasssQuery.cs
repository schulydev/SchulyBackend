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
    public record GetClassesQuery() : IQuery<Result<List<ClassDto>>>;

    public class GetClassesQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetClassesQuery, Result<List<ClassDto>>>
    {
        public async ValueTask<Result<List<ClassDto>>> Handle(GetClassesQuery query, CancellationToken cancellationToken)
        {
            var baseQuery = dbContext.Classes.AsNoTracking().AsSplitQuery();

            List<Domain.Class> classes;
            if (userService.IsCurrentUserAdmin())
            {
                classes = await baseQuery
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Absences)
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Grades)
                    .Include(c => c.Agenda)
                    .Include(c => c.Exams)
                        .ThenInclude(e => e.Grades)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                // Students only see classes they're enrolled in, and only their
                // own grades/absences within the roster — never a classmate's.
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                classes = await baseQuery
                    .Where(c => c.Students.Any(su => myIds.Contains(su.Id)))
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Absences.Where(a => myIds.Contains(a.SchoolUserId)))
                    .Include(c => c.Students)
                        .ThenInclude(s => s.Grades.Where(g => myIds.Contains(g.SchoolUserId)))
                    .Include(c => c.Agenda)
                    .Include(c => c.Exams)
                        .ThenInclude(e => e.Grades.Where(g => myIds.Contains(g.SchoolUserId)))
                    .ToListAsync(cancellationToken);
            }

            var dtos = classes.ToDto();
            foreach (var student in dtos.SelectMany(c => c.Students))
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<List<ClassDto>>.Success(dtos);
        }
    }
}
