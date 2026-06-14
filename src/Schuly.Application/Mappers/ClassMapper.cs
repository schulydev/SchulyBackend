using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class ClassMapper
    {
        public static ClassDto ToDto(this Class classEntity)
        {
            return new ClassDto
            {
                Id = classEntity.Id,
                Name = classEntity.Name,
                Description = classEntity.Description,
                SchoolId = classEntity.SchoolId,
                SchoolName = classEntity.School?.Name,
                Students = classEntity.Students.Select(s => s.ToDto()).ToList(),
                Agenda = classEntity.Agenda.Select(a => a.ToDto()).ToList(),
                Exams = classEntity.Exams.Select(e => e.ToDto()).ToList()
            };
        }

        public static List<ClassDto> ToDto(this List<Class> classes)
        {
            return classes.Select(c => c.ToDto()).ToList();
        }
    }
}
