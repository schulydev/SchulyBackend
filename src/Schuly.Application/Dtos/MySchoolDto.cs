namespace Schuly.Application.Dtos
{
    /// <summary>
    /// Lean per-user view of a school the current user belongs to. Combines the
    /// school's identity with the user's own profile fields — intentionally not
    /// the full <see cref="SchoolDto"/>, which is for admin school management.
    /// </summary>
    public class MySchoolDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string FullName { get; set; }
        public string? LogoUrl { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
