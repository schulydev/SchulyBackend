using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.StudentDocument
{
    [AllowAuthenticated]
    public record GetStudentDocumentsQuery() : IQuery<Result<List<StudentDocumentDto>>>;

    public class GetStudentDocumentsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetStudentDocumentsQuery, Result<List<StudentDocumentDto>>>
    {
        public async ValueTask<Result<List<StudentDocumentDto>>> Handle(GetStudentDocumentsQuery query, CancellationToken cancellationToken)
        {
            var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);

            var dbQuery = dbContext.StudentDocuments
                .AsNoTracking()
                .Include(d => d.SchoolUser)
                .Where(d => d.SchoolUser!.ApplicationUserId == currentUserId);

            var documents = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<StudentDocumentDto>>.Success(documents.ToDto());
        }
    }
}
