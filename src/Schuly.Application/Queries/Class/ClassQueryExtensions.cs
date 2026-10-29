using Microsoft.EntityFrameworkCore;

namespace Schuly.Application.Queries.Class
{
    internal static class ClassQueryExtensions
    {
        public static IQueryable<Domain.Class> IncludeRoster(this IQueryable<Domain.Class> query, IReadOnlyList<Guid> mySchoolUserIds) =>
            query
                .Include(c => c.Students.Where(s => mySchoolUserIds.Contains(s.Id)))
                    .ThenInclude(s => s.Absences)
                .Include(c => c.Students.Where(s => mySchoolUserIds.Contains(s.Id)))
                    .ThenInclude(s => s.Grades)
                .Include(c => c.Agenda.Where(a => a.SchoolUserId != null && mySchoolUserIds.Contains(a.SchoolUserId.Value)))
                .Include(c => c.Exams)
                    .ThenInclude(e => e.Grades.Where(g => mySchoolUserIds.Contains(g.SchoolUserId)));
    }
}
