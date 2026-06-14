using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using Schuly.Infrastructure.Storage;

namespace Schuly.Application.Queries.StudentDocument
{
    public record DocumentDownload(DocumentStream Stream, string FileName);

    /// <summary>
    /// Open a document for download. Authorization rule mirrors
    /// GetSchoolUserQuery: owner-only, with an Administrator escape hatch.
    /// The backend proxies the bytes — the client never talks to S3.
    /// </summary>
    [AllowAuthenticated]
    public record OpenStudentDocumentQuery(Guid DocumentId) : IQuery<Result<DocumentDownload>>;

    public class OpenStudentDocumentQueryHandler(
        SchulyDbContext db,
        IDocumentStorage storage,
        IUserService userService) : IQueryHandler<OpenStudentDocumentQuery, Result<DocumentDownload>>
    {
        public async ValueTask<Result<DocumentDownload>> Handle(OpenStudentDocumentQuery query, CancellationToken ct)
        {
            var doc = await db.StudentDocuments
                .AsNoTracking()
                .Include(d => d.SchoolUser)
                .SingleOrDefaultAsync(d => d.Id == query.DocumentId, ct);
            if (doc is null)
                return Result<DocumentDownload>.Failure("Document not found");

            if (!userService.IsCurrentUserAdmin())
            {
                var currentUserId = await userService.GetCurrentUserIdAsync(ct);
                if (doc.SchoolUser?.ApplicationUserId != currentUserId)
                    return Result<DocumentDownload>.Forbidden();
            }

            if (string.IsNullOrWhiteSpace(doc.FileUrl))
                return Result<DocumentDownload>.Failure("Document has no stored file");

            var stream = await storage.OpenReadAsync(doc.FileUrl, ct);
            return Result<DocumentDownload>.Success(new DocumentDownload(stream, doc.FileName ?? "document"));
        }
    }
}
