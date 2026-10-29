using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.School
{
    [AllowAuthenticated]
    public record GetSchoolQuery(Guid SchoolId) : IQuery<Result<SchoolDto>>;

    public class GetSchoolQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetSchoolQuery, Result<SchoolDto>>
    {
        public async ValueTask<Result<SchoolDto>> Handle(GetSchoolQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var school = await dbContext.Schools
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == query.SchoolId && s.SchoolUsers.Any(su => su.ApplicationUserId == userId), cancellationToken);

            if (school == null)
                return Result<SchoolDto>.NotFound($"School with ID '{query.SchoolId}' not found");

            return Result<SchoolDto>.Success(school.ToDto());
        }
    }
}
