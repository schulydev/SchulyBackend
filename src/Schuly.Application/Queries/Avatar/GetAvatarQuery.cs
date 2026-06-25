using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Storage;

namespace Schuly.Application.Queries.Avatar
{
    public record AvatarDownload(DocumentStream Stream);

    /// <summary>
    /// Streams a SchoolUser's stored avatar blob. Access is gated by the HMAC
    /// signature validated in the controller (the endpoint is anonymous), so no
    /// per-user check here — <see cref="AllowAuthenticatedAttribute"/> only
    /// bypasses the role gate in the authorization behavior.
    /// </summary>
    [AllowAuthenticated]
    public record GetAvatarQuery(Guid SchoolUserId) : IQuery<Result<AvatarDownload>>;

    public class GetAvatarQueryHandler(SchulyDbContext db, IDocumentStorage storage) : IQueryHandler<GetAvatarQuery, Result<AvatarDownload>>
    {
        public async ValueTask<Result<AvatarDownload>> Handle(GetAvatarQuery query, CancellationToken ct)
        {
            var key = await db.SchoolUsers
                .AsNoTracking()
                .Where(su => su.Id == query.SchoolUserId)
                .Select(su => su.ProfilePictureUrl)
                .SingleOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(key) ||
                key.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Result<AvatarDownload>.Failure("No stored avatar for this user");

            var stream = await storage.OpenReadAsync(key, ct);
            return Result<AvatarDownload>.Success(new AvatarDownload(stream));
        }
    }
}
