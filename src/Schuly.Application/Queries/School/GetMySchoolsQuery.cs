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

    public class GetMySchoolsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetMySchoolsQuery, Result<List<MySchoolDto>>>
    {
        public async ValueTask<Result<List<MySchoolDto>>> Handle(GetMySchoolsQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var schools = await dbContext.SchoolUsers
                .AsNoTracking()
                .Where(su => su.ApplicationUserId == userId && su.School != null)
                .Select(su => new MySchoolDto
                {
                    Id = su.School!.Id,
                    Name = su.School.Name,
                    Email = su.Email,
                    FullName = (su.FirstName + " " + su.LastName).Trim(),
                })
                .ToListAsync(cancellationToken);

            return Result<List<MySchoolDto>>.Success(schools);
        }
    }
}
