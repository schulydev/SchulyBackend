namespace Schuly.Domain
{
    public class Teacher : Base
    {
        public Guid SchoolId { get; set; }
        public School? School { get; set; }

        // Optional link to the login that owns this teacher record. Null for
        // teacher rows imported without a matching account.
        public Guid? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Code { get; set; }
        public string? Email { get; set; }

        public ICollection<Class> Classes { get; set; } = [];
    }
}
