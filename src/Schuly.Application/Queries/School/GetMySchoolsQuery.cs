using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.School
{
    [AuthorizedRoles(Roles.Student)]
    public record GetMySchoolsQuery() : IQuery<Result<List<MySchoolDto>>>;

    public class GetMySchoolsQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetMySchoolsQuery, Result<List<MySchoolDto>>>
    {
        public async ValueTask<Result<List<MySchoolDto>>> Handle(GetMySchoolsQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var rows = await dbContext.SchoolUsers
                .AsNoTracking()
                .Where(su => su.ApplicationUserId == userId && su.School != null)
                .Select(su => new
                {
                    su.Id,
                    SchoolId = su.School!.Id,
                    su.School.Name,
                    su.Email,
                    su.FirstName,
                    su.LastName,
                    su.School.LogoUrl,
                    su.ProfilePictureUrl,
                })
                .ToListAsync(cancellationToken);

            var schools = rows.Select(r => new MySchoolDto
            {
                Id = r.SchoolId,
                Name = r.Name,
                Email = r.Email,
                FullName = (r.FirstName + " " + r.LastName).Trim(),
                LogoUrl = r.LogoUrl,
                ProfilePictureUrl = avatarSigner.ToPublicUrl(r.Id, r.ProfilePictureUrl),
            }).ToList();

            return Result<List<MySchoolDto>>.Success(schools);
        }
    }
}
