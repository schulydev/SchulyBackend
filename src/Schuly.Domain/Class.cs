namespace Schuly.Domain
{
    public class Class : Base
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public string? DisplayName { get; set; }
        public string? Type { get; set; }
        public int? SchoolYearStart { get; set; }
        public int? SemesterHalf { get; set; }

        public Guid SchoolId { get; set; }
        public School? School { get; set; }
        public ICollection<SchoolUser> Students { get; set; } = [];
        public ICollection<Teacher> Teachers { get; set; } = [];
        public ICollection<AgendaEntry> Agenda { get; set; } = [];
        public ICollection<Exam> Exams { get; set; } = [];
    }
}
