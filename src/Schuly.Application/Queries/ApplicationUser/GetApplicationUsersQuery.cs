using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;

namespace Schuly.Application.Queries.ApplicationUser
{
    [AuthorizedRoles(Roles.Administrator)]
    public record GetApplicationUsersQuery() : IQuery<Result<List<ApplicationUserDto>>>;

    public class GetApplicationUsersQueryHandler(SchulyDbContext dbContext) : IQueryHandler<GetApplicationUsersQuery, Result<List<ApplicationUserDto>>>
    {
        public async ValueTask<Result<List<ApplicationUserDto>>> Handle(GetApplicationUsersQuery query, CancellationToken cancellationToken)
        {
            var applicationUsers = await dbContext.ApplicationUsers
                .Include(au => au.SchoolUsers)
                .ToListAsync(cancellationToken);

            return Result<List<ApplicationUserDto>>.Success(applicationUsers.ToDto());
        }
    }
}
