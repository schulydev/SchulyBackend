using Microsoft.EntityFrameworkCore;

namespace Schuly.Application.Queries.Class
{
    internal static class ClassQueryExtensions
    {
        public static IQueryable<Domain.Class> IncludeRoster(this IQueryable<Domain.Class> query, bool isAdmin, IReadOnlyList<Guid> mySchoolUserIds) =>
            query
                .Include(c => c.Students)
                    .ThenInclude(s => s.Absences.Where(a => isAdmin || mySchoolUserIds.Contains(a.SchoolUserId)))
                .Include(c => c.Students)
                    .ThenInclude(s => s.Grades.Where(g => isAdmin || mySchoolUserIds.Contains(g.SchoolUserId)))
                .Include(c => c.Agenda)
                .Include(c => c.Exams)
                    .ThenInclude(e => e.Grades.Where(g => isAdmin || mySchoolUserIds.Contains(g.SchoolUserId)));
    }
}
