using Schuly.Domain.Enums;

namespace Schuly.Application.Dtos
{
    public class ClassMemberDto
    {
        public Guid Id { get; set; }
        public Guid SchoolId { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public required Roles Role { get; set; }
        public List<AbsenceDto> Absences { get; set; } = new List<AbsenceDto>();
        public List<GradeDto> Grades { get; set; } = new List<GradeDto>();
    }
}
