using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Storage;

namespace Schuly.Application.Commands.StudentDocument
{
    [AuthorizedRoles(Roles.Administrator)]
    public record ReencryptStudentDocumentsCommand : ICommand<Result<ReencryptStudentDocumentsResult>>;

    public record ReencryptStudentDocumentsResult(int Total, int Reencrypted, int AlreadyEncrypted, int Failed);

    public class ReencryptStudentDocumentsCommandHandler(SchulyDbContext db, IDocumentStorage storage, IDocumentEncryptionMaintenance maintenance, ILogger<ReencryptStudentDocumentsCommandHandler> logger) : ICommandHandler<ReencryptStudentDocumentsCommand, Result<ReencryptStudentDocumentsResult>>
    {
        public async ValueTask<Result<ReencryptStudentDocumentsResult>> Handle(ReencryptStudentDocumentsCommand command, CancellationToken ct)
        {
            var documents = await db.StudentDocuments
                .Where(d => d.FileUrl != null && d.FileUrl != "")
                .ToListAsync(ct);

            var reencrypted = 0;
            var alreadyEncrypted = 0;
            var failed = 0;

            foreach (var doc in documents)
            {
                try
                {
                    var result = await maintenance.ReencryptAsync(doc.FileUrl!, ct);
                    if (result.WasAlreadyEncrypted)
                    {
                        alreadyEncrypted++;
                        continue;
                    }

                    var oldKey = doc.FileUrl!;
                    doc.FileUrl = result.Key;
                    await db.SaveChangesAsync(ct);
                    await storage.DeleteAsync(oldKey, ct);
                    reencrypted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogWarning(ex, "Failed to re-encrypt student document {DocumentId}", doc.Id);
                }
            }

            return Result<ReencryptStudentDocumentsResult>.Success(new ReencryptStudentDocumentsResult(documents.Count, reencrypted, alreadyEncrypted, failed));
        }
    }
}
