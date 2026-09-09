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

            var isAdmin = userService.IsCurrentUserAdmin();
            var currentUserId = isAdmin ? Guid.Empty : await userService.GetCurrentUserIdAsync(cancellationToken);

            if (isAdmin)
            {
                if (query.ApplicationUserId.HasValue)
                    dbQuery = dbQuery.Where(su => su.ApplicationUserId == query.ApplicationUserId.Value);
            }
            else if (userService.IsCurrentUserTeacher())
            {
                var mySchoolIds = await dbContext.Teachers
                    .AsNoTracking()
                    .Where(t => t.ApplicationUserId == currentUserId)
                    .Select(t => t.SchoolId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                dbQuery = dbQuery.Where(su => mySchoolIds.Contains(su.SchoolId));
                if (query.ApplicationUserId.HasValue)
                    dbQuery = dbQuery.Where(su => su.ApplicationUserId == query.ApplicationUserId.Value);
            }
            else
            {
                dbQuery = dbQuery.Where(su => su.ApplicationUserId == currentUserId);
            }

            var schoolUsers = await dbQuery.ToListAsync(cancellationToken);
            var dtos = schoolUsers.Select(su => isAdmin || su.ApplicationUserId == currentUserId ? su.ToDto() : su.ToSummaryDto()).ToList();
            foreach (var dto in dtos)
                dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<List<SchoolUserDto>>.Success(dtos);
        }
    }
}
