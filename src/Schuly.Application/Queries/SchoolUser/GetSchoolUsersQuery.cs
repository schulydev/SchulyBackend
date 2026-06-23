using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.SchoolUser
{
    // Any authenticated user may call this; the handler scopes the result by role:
    // admins see all, teachers see users at their own school(s), and everyone else
    // (students) sees only their own SchoolUsers — so the app can load a student's
    // own profile without needing the Teacher role.
    [AllowAuthenticated]
    public record GetSchoolUsersQuery(Guid? ApplicationUserId = null) : IQuery<Result<List<SchoolUserDto>>>;

    public class GetSchoolUsersQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetSchoolUsersQuery, Result<List<SchoolUserDto>>>
    {
        public async ValueTask<Result<List<SchoolUserDto>>> Handle(GetSchoolUsersQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.SchoolUsers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(su => su.Absences)
                .Include(su => su.Grades)
                .Include(su => su.Classes)
                .AsQueryable();

            if (userService.IsCurrentUserAdmin())
            {
                // Admins see everything; honour the optional filter.
                if (query.ApplicationUserId.HasValue)
                    dbQuery = dbQuery.Where(su => su.ApplicationUserId == query.ApplicationUserId.Value);
            }
            else if (userService.IsCurrentUserTeacher())
            {
                // A teacher only sees users at the school(s) they belong to.
                var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
                var mySchoolIds = await dbContext.SchoolUsers
                    .AsNoTracking()
                    .Where(su => su.ApplicationUserId == userId)
                    .Select(su => su.SchoolId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                dbQuery = dbQuery.Where(su => mySchoolIds.Contains(su.SchoolId));
                if (query.ApplicationUserId.HasValue)
                    dbQuery = dbQuery.Where(su => su.ApplicationUserId == query.ApplicationUserId.Value);
            }
            else
            {
                // Everyone else (students) only ever sees their own SchoolUsers,
                // regardless of the requested filter — no cross-user access.
                var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
                dbQuery = dbQuery.Where(su => su.ApplicationUserId == userId);
            }

            var schoolUsers = await dbQuery.ToListAsync(cancellationToken);
            var dtos = schoolUsers.ToDto();
            foreach (var dto in dtos)
                dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<List<SchoolUserDto>>.Success(dtos);
        }
    }
}
