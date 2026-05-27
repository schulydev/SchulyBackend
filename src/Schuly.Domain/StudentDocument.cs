namespace Schuly.Domain
{
    public class StudentDocument : Base
    {
        public Guid SchoolUserId { get; set; }
        public SchoolUser? SchoolUser { get; set; }

        public required string Title { get; set; }
        public string? Comment { get; set; }
        public string? Category { get; set; }
        public string? EnteredBy { get; set; }

        public string? FileName { get; set; }
        public string? FileUrl { get; set; }
        public long? FileSizeBytes { get; set; }

        public string? FollowUpAction { get; set; }
        public DateOnly? FollowUpDate { get; set; }
        public DateOnly? CompletedDate { get; set; }
        public DateTime? NotifiedAt { get; set; }
    }
}
