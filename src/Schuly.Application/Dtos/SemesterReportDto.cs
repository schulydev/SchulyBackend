namespace Schuly.Application.Dtos
{
    public class SemesterReportDto
    {
        public Guid Id { get; set; }
        public Guid SchoolUserId { get; set; }
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
        public List<SemesterSubjectGradeDto> Subjects { get; set; } = new List<SemesterSubjectGradeDto>();
    }
}
