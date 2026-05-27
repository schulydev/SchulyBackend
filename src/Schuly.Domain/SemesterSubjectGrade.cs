namespace Schuly.Domain
{
    public class SemesterSubjectGrade : Base
    {
        public Guid SemesterReportId { get; set; }
        public SemesterReport? SemesterReport { get; set; }

        public required string SubjectCode { get; set; }
        public required string SubjectName { get; set; }
        public string? SubjectTypeMarker { get; set; }

        public decimal? Grade { get; set; }
        public string? Marker { get; set; }
    }
}
