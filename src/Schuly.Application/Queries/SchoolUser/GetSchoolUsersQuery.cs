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
    public record GetSchoolUsersQuery() : IQuery<Result<List<SchoolUserDto>>>;

    public class GetSchoolUsersQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetSchoolUsersQuery, Result<List<SchoolUserDto>>>
    {
        public async ValueTask<Result<List<SchoolUserDto>>> Handle(GetSchoolUsersQuery query, CancellationToken cancellationToken)
        {
            var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var schoolUsers = await dbContext.SchoolUsers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(su => su.Absences)
                .Include(su => su.Grades)
                .Include(su => su.Classes)
                .Where(su => su.ApplicationUserId == currentUserId)
                .ToListAsync(cancellationToken);

            var dtos = schoolUsers.Select(su => su.ToDto()).ToList();

            foreach (var dto in dtos)
                dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<List<SchoolUserDto>>.Success(dtos);
        }
    }
}
