using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Application.Authorization;

namespace Schuly.Application.Queries.Absence
{
    [AllowAuthenticated]
    public record GetAbsenceQuery(Guid AbsenceId) : IQuery<Result<AbsenceDto>>;

    public class GetAbsenceQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetAbsenceQuery, Result<AbsenceDto>>
    {
        public async ValueTask<Result<AbsenceDto>> Handle(GetAbsenceQuery query, CancellationToken cancellationToken)
        {
            var absence = await dbContext.Absences
                .AsNoTracking()
                .Include(a => a.SchoolUser)
                .SingleOrDefaultAsync(a => a.Id == query.AbsenceId, cancellationToken);

            if (absence == null)
                return Result<AbsenceDto>.Failure($"Absence with ID '{query.AbsenceId}' not found");

            // Non-admins can only read their own absences.
            if (!userService.IsCurrentUserAdmin())
            {
                var myIds = await userService.GetCurrentUserSchoolUserIdsAsync(cancellationToken);
                if (!myIds.Contains(absence.SchoolUserId))
                    return Result<AbsenceDto>.Failure("Forbidden");
            }

            return Result<AbsenceDto>.Success(absence.ToDto());
        }
    }
}
