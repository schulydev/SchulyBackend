namespace Schuly.Application.Dtos
{
    public class SemesterSubjectGradeDto
    {
        public Guid Id { get; set; }
        public Guid SemesterReportId { get; set; }
        public required string SubjectCode { get; set; }
        public required string SubjectName { get; set; }
        public string? SubjectTypeMarker { get; set; }
        public decimal? Grade { get; set; }
        public string? Marker { get; set; }
    }
}
