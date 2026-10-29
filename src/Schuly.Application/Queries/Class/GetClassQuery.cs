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
    public record GetClassQuery(Guid ClassId) : IQuery<Result<ClassDto>>;

    public class GetClassQueryHandler(SchulyDbContext dbContext, IUserService userService, IAvatarUrlSigner avatarSigner) : IQueryHandler<GetClassQuery, Result<ClassDto>>
    {
        public async ValueTask<Result<ClassDto>> Handle(GetClassQuery query, CancellationToken cancellationToken)
        {
            var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var dbQuery = dbContext.Classes
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => c.Id == query.ClassId && c.Students.Any(su => myIds.Contains(su.Id)));

            var classEntity = await dbQuery
                .IncludeRoster(myIds)
                .SingleOrDefaultAsync(cancellationToken);

            if (classEntity == null)
                return Result<ClassDto>.NotFound($"Class with ID '{query.ClassId}' not found");

            var averages = await dbContext.Grades.ToClassAveragesAsync(classEntity.Exams.Select(e => e.Id).ToList(), cancellationToken);
            var dto = classEntity.ToDto(averages);
            foreach (var student in dto.Students)
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<ClassDto>.Success(dto);
        }
    }
}
