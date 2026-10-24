using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.LinkedAccount
{
    /// <summary>The signed-in user's linked accounts, optionally only one provider's.</summary>
    [AllowAuthenticated]
    public record GetMyLinkedAccountsQuery(string? Provider) : IQuery<Result<List<LinkedAccountDto>>>;

    public class GetMyLinkedAccountsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetMyLinkedAccountsQuery, Result<List<LinkedAccountDto>>>
    {
        public async ValueTask<Result<List<LinkedAccountDto>>> Handle(GetMyLinkedAccountsQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var accounts = await dbContext.LinkedAccounts
                .AsNoTracking()
                .Where(a => a.ApplicationUserId == userId && (query.Provider == null || a.Provider == query.Provider))
                .OrderBy(a => a.Provider).ThenBy(a => a.Email)
                .Select(a => new LinkedAccountDto { Id = a.Id, Provider = a.Provider, Email = a.Email, DisplayName = a.DisplayName, CreatedAt = a.CreatedAt })
                .ToListAsync(cancellationToken);
            return Result<List<LinkedAccountDto>>.Success(accounts);
        }
    }
}
