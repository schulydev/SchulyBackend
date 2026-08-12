namespace Schuly.Application.Dtos
{
    public class StudentDocumentDto
    {
        public Guid Id { get; set; }
        public Guid SchoolUserId { get; set; }
        public required string Title { get; set; }
        public string? Comment { get; set; }
        public string? Category { get; set; }
        public string? EnteredBy { get; set; }
        public string? FileName { get; set; }
        public long? FileSizeBytes { get; set; }
        public string? FollowUpAction { get; set; }
        public DateOnly? FollowUpDate { get; set; }
        public DateOnly? CompletedDate { get; set; }
        public DateTime? NotifiedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
