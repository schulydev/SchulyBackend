using System.Linq.Expressions;
using Schuly.Domain;

namespace Schuly.Application.Queries.Agenda
{
    internal static class AgendaVisibility
    {
        public static Expression<Func<AgendaEntry, bool>> For(IReadOnlyList<Guid> schoolUserIds) =>
            e => (e.SchoolUserId != null && schoolUserIds.Contains(e.SchoolUserId.Value))
              || (e.ClassId != null && e.Class!.Students.Any(su => schoolUserIds.Contains(su.Id)))
              || (e.SchoolId != null && e.School!.SchoolUsers.Any(su => schoolUserIds.Contains(su.Id)));
    }
}
