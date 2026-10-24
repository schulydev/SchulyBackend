using Schuly.Application.Models;
using Schuly.Domain;

namespace Schuly.Application.Commands
{
    /// <summary>Checks shared by every hand-entered write: no edits to synced rows, no times without an offset.</summary>
    public static class ManualEdit
    {
        public static Result? RefuseSynced(ISyncedEntity row) =>
            row.Source is null ? null : Result.Forbidden($"This entry is synced from {row.Source}; change it there.");

        public static Result? RequireOffset(params DateTime?[] values) =>
            values.Any(v => v is { Kind: DateTimeKind.Unspecified })
                ? Result.Failure("Times must include a UTC offset, e.g. 2026-10-23T06:00:00Z.")
                : null;
    }
}
