namespace Schuly.Domain
{
    public class SemesterReport : Base
    {
        public Guid SchoolUserId { get; set; }
        public SchoolUser? SchoolUser { get; set; }

        public required string ProgramCode { get; set; }
        public required int SchoolYearStart { get; set; }
        public required int SemesterHalf { get; set; }
        public required string ClassName { get; set; }

        public string? PromotionDecision { get; set; }
        public decimal? GradeAverage { get; set; }
        public int? InsufficientGradeCount { get; set; }
        public int? DeficiencyPoints { get; set; }

        public int? ExcusedAbsences { get; set; }
        public int? UnexcusedAbsences { get; set; }
        public int? TotalAbsences { get; set; }

        public ICollection<SemesterSubjectGrade> Subjects { get; set; } = [];
    }
}
