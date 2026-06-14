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
            // Students only see classes they're enrolled in, and only their own
            // grades/absences within the roster — never a classmate's. Admins see all.
            var isAdmin = userService.IsCurrentUserAdmin();
            IReadOnlyList<Guid> myIds = isAdmin ? [] : await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var dbQuery = dbContext.Classes.AsNoTracking().AsSplitQuery();

            if (!isAdmin)
                dbQuery = dbQuery.Where(c => c.Students.Any(su => myIds.Contains(su.Id)));

            var classes = await dbQuery
                .IncludeRoster(isAdmin, myIds)
                .ToListAsync(cancellationToken);

            var dtos = classes.ToDto();
            foreach (var student in dtos.SelectMany(c => c.Students))
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<List<ClassDto>>.Success(dtos);
        }
    }
}
