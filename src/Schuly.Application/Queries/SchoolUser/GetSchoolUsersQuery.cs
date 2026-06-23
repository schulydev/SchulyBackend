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
    [AuthorizedRoles(Roles.Teacher)]
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

            // A teacher only sees users at the school(s) they themselves belong to,
            // not every school in the system; admins see all.
            if (!userService.IsCurrentUserAdmin())
            {
                var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
                var mySchoolIds = await dbContext.SchoolUsers
                    .AsNoTracking()
                    .Where(su => su.ApplicationUserId == userId)
                    .Select(su => su.SchoolId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                dbQuery = dbQuery.Where(su => mySchoolIds.Contains(su.SchoolId));
            }

            if (query.ApplicationUserId.HasValue)
                dbQuery = dbQuery.Where(su => su.ApplicationUserId == query.ApplicationUserId.Value);

            var schoolUsers = await dbQuery.ToListAsync(cancellationToken);
            var dtos = schoolUsers.ToDto();
            foreach (var dto in dtos)
                dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<List<SchoolUserDto>>.Success(dtos);
        }
    }
}
