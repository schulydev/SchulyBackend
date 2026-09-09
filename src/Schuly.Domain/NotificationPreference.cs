namespace Schuly.Domain
{
    public class NotificationPreference : Base
    {
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public bool Grades { get; set; } = true;
        public bool Absences { get; set; } = true;
        public bool Agenda { get; set; } = true;
        public bool IncludeGradeValue { get; set; } = false;
    }
}
