using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class ExamMapper
    {
        public static ExamDto ToDto(this Exam exam)
        {
            return new ExamDto
            {
                Id = exam.Id,
                Name = exam.Name,
                Description = exam.Description,
                Type = exam.Type,
                Date = exam.Date,
                ClassAverage = exam.Grades.Any() ? exam.Grades.Sum(g => g.Score) / exam.Grades.Count : 0,
                ClassId = exam.ClassId,
                SchoolId = exam.Class?.SchoolId,
                Grades = exam.Grades.Select(g => g.ToDto()).ToList()
            };
        }

        public static List<ExamDto> ToDto(this List<Exam> exams)
        {
            return exams.Select(e => e.ToDto()).ToList();
        }
    }
}
