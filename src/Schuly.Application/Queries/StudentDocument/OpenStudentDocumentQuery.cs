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

    [AllowAuthenticated]
    public record OpenStudentDocumentQuery(Guid DocumentId) : IQuery<Result<DocumentDownload>>;

    public class OpenStudentDocumentQueryHandler(SchulyDbContext db, IDocumentStorage storage, IUserService userService) : IQueryHandler<OpenStudentDocumentQuery, Result<DocumentDownload>>
    {
        public async ValueTask<Result<DocumentDownload>> Handle(OpenStudentDocumentQuery query, CancellationToken ct)
        {
            var currentUserId = await userService.GetCurrentUserIdAsync(ct);

            var doc = await db.StudentDocuments
                .AsNoTracking()
                .SingleOrDefaultAsync(d => d.Id == query.DocumentId && d.SchoolUser!.ApplicationUserId == currentUserId, ct);
            if (doc is null)
                return Result<DocumentDownload>.NotFound("Document not found");

            if (string.IsNullOrWhiteSpace(doc.FileUrl))
                return Result<DocumentDownload>.Failure("Document has no stored file");

            var stream = await storage.OpenReadAsync(doc.FileUrl, ct);
            return Result<DocumentDownload>.Success(new DocumentDownload(stream, doc.FileName ?? "document"));
        }
    }
}
