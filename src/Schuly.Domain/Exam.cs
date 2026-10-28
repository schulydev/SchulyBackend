using Schuly.Domain.Enums;

namespace Schuly.Domain
{
    public class Exam : Base
    {
        public string? Source { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public ExamType Type { get; set; }
        public DateOnly? Date { get; set; }
        public decimal? ReportedClassAverage { get; set; }

        public Guid ClassId { get; set; }
        public Class? Class { get; set; }
        public ICollection<Grade> Grades { get; set; } = [];
    }
}
