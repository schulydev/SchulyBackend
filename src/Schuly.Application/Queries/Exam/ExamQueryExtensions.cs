using Microsoft.EntityFrameworkCore;

namespace Schuly.Application.Queries.Exam
{
    internal static class ExamQueryExtensions
    {
        public static IQueryable<Domain.Exam> ApplyVisibility(this IQueryable<Domain.Exam> exams, bool isAdmin, IReadOnlyList<Guid> myIds)
        {
            IQueryable<Domain.Exam> scoped = exams.AsNoTracking().Include(e => e.Class);

            if (!isAdmin)
                scoped = scoped.Where(e => e.Class!.Students.Any(su => myIds.Contains(su.Id)));

            return scoped.Include(e => e.Grades.Where(g => isAdmin || myIds.Contains(g.SchoolUserId)));
        }
    }
}
