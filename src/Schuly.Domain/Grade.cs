namespace Schuly.Domain
{
    public class Grade : Base, ISyncedEntity
    {
        public string? Source { get; set; }
        public string? ExternalId { get; set; }

        public decimal Score { get; set; }
        public decimal Weighting { get; set; }

        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }
        public Guid SchoolUserId { get; set; }
        public SchoolUser? SchoolUser { get; set; }
    }
}
