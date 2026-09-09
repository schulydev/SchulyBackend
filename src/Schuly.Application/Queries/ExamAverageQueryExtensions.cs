using Microsoft.EntityFrameworkCore;
using Schuly.Domain;

namespace Schuly.Application.Queries
{
    internal static class ExamAverageQueryExtensions
    {
        // Deliberately aggregates over ALL grades of the exam rather than the caller-filtered
        // Exam.Grades collection, so a non-admin still gets a real class average. A scalar
        // aggregate leaks no individual grade to the caller.
        public static async Task<Dictionary<Guid, decimal>> ToClassAveragesAsync(this IQueryable<Grade> grades, IReadOnlyCollection<Guid> examIds, CancellationToken cancellationToken)
        {
            if (examIds.Count == 0)
                return [];

            var aggregates = await grades
                .Where(g => examIds.Contains(g.ExamId))
                .GroupBy(g => g.ExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    WeightedSum = g.Sum(x => x.Score * x.Weighting),
                    TotalWeight = g.Sum(x => x.Weighting),
                    PlainSum = g.Sum(x => x.Score),
                    Count = g.Count()
                })
                .ToListAsync(cancellationToken);

            // A group always holds at least one grade, so the unweighted fallback is safe
            // when every grade of an exam carries a zero weighting.
            return aggregates.ToDictionary(
                a => a.ExamId,
                a => a.TotalWeight > 0 ? a.WeightedSum / a.TotalWeight : a.PlainSum / a.Count);
        }
    }
}
