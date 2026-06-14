using System.Linq.Expressions;
using Schuly.Domain;

namespace Schuly.Application.Queries.Agenda
{
    internal static class AgendaVisibility
    {
        /// <summary>
        /// An agenda entry is visible to a non-admin when it is their personal
        /// entry, belongs to a class they're enrolled in, or targets a school
        /// they belong to. <paramref name="schoolUserIds"/> are the caller's own
        /// <see cref="SchoolUser"/> ids.
        /// </summary>
        public static Expression<Func<AgendaEntry, bool>> For(IReadOnlyList<Guid> schoolUserIds) =>
            e => (e.SchoolUserId != null && schoolUserIds.Contains(e.SchoolUserId.Value))
              || (e.ClassId != null && e.Class!.Students.Any(su => schoolUserIds.Contains(su.Id)))
              || (e.SchoolId != null && e.School!.SchoolUsers.Any(su => schoolUserIds.Contains(su.Id)));
    }
}
