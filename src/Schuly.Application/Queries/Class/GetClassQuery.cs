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
            // Students only see a class they're enrolled in, and only their own
            // grades/absences within the roster — never a classmate's. Admins see all.
            var isAdmin = userService.IsCurrentUserAdmin();
            IReadOnlyList<Guid> myIds = isAdmin ? [] : await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);

            var dbQuery = dbContext.Classes
                .AsNoTracking()
                .AsSplitQuery()
                .Where(c => c.Id == query.ClassId);

            if (!isAdmin)
                dbQuery = dbQuery.Where(c => c.Students.Any(su => myIds.Contains(su.Id)));

            var classEntity = await dbQuery
                .IncludeRoster(isAdmin, myIds)
                .SingleOrDefaultAsync(cancellationToken);

            if (classEntity == null)
                return Result<ClassDto>.Failure($"Class with ID '{query.ClassId}' not found");

            var dto = classEntity.ToDto();
            foreach (var student in dto.Students)
                student.ProfilePictureUrl = avatarSigner.ToPublicUrl(student.Id, student.ProfilePictureUrl);
            return Result<ClassDto>.Success(dto);
        }
    }
}
