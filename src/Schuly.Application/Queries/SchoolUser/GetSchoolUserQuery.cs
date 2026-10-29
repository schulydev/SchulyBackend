using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.SchoolUser
{
    [AllowAuthenticated]
    public record GetSchoolUserQuery(Guid SchoolUserId) : IQuery<Result<SchoolUserDto>>;

    public class GetSchoolUserQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetSchoolUserQuery, Result<SchoolUserDto>>
    {
        public async ValueTask<Result<SchoolUserDto>> Handle(GetSchoolUserQuery query, CancellationToken cancellationToken)
        {
            var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var schoolUser = await dbContext.SchoolUsers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(su => su.Absences)
                .Include(su => su.Grades)
                .Include(su => su.Classes)
                .SingleOrDefaultAsync(su => su.Id == query.SchoolUserId && su.ApplicationUserId == currentUserId, cancellationToken);

            if (schoolUser == null)
                return Result<SchoolUserDto>.NotFound($"SchoolUser with ID '{query.SchoolUserId}' not found");

            var dto = schoolUser.ToDto();
            dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<SchoolUserDto>.Success(dto);
        }
    }
}
