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
    /// <summary>
    /// Lists document metadata (no file bytes). Owner-only, with an
    /// Administrator escape hatch — mirrors <see cref="OpenStudentDocumentQuery"/>.
    /// Download the actual content via GET /api/documents/{id}.
    /// </summary>
    [AllowAuthenticated]
    public record GetStudentDocumentsQuery(Guid? SchoolUserId = null) : IQuery<Result<List<StudentDocumentDto>>>;

    public class GetStudentDocumentsQueryHandler(SchulyDbContext dbContext, IUserService userService) : IQueryHandler<GetStudentDocumentsQuery, Result<List<StudentDocumentDto>>>
    {
        public async ValueTask<Result<List<StudentDocumentDto>>> Handle(GetStudentDocumentsQuery query, CancellationToken cancellationToken)
        {
            var dbQuery = dbContext.StudentDocuments
                .AsNoTracking()
                .Include(d => d.SchoolUser)
                .AsQueryable();

            if (query.SchoolUserId.HasValue)
                dbQuery = dbQuery.Where(d => d.SchoolUserId == query.SchoolUserId.Value);

            if (!userService.IsCurrentUserAdmin())
            {
                var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);
                dbQuery = dbQuery.Where(d => d.SchoolUser!.ApplicationUserId == currentUserId);
            }

            var documents = await dbQuery.ToListAsync(cancellationToken);
            return Result<List<StudentDocumentDto>>.Success(documents.ToDto());
        }
    }
}
