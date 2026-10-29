using Microsoft.EntityFrameworkCore;

namespace Schuly.Application.Queries.Exam
{
    internal static class ExamQueryExtensions
    {
        public static IQueryable<Domain.Exam> ApplyVisibility(this IQueryable<Domain.Exam> exams, IReadOnlyList<Guid> myIds)
        {
            return exams
                .AsNoTracking()
                .Include(e => e.Class)
                .Where(e => e.Class!.Students.Any(su => myIds.Contains(su.Id)))
                .Include(e => e.Grades.Where(g => myIds.Contains(g.SchoolUserId)));
        }
    }
}
