using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class ExamMapper
    {
        public static ExamDto ToDto(this Exam exam, IReadOnlyDictionary<Guid, decimal> classAverages)
        {
            return new ExamDto
            {
                Id = exam.Id,
                Name = exam.Name,
                Description = exam.Description,
                Type = exam.Type,
                Date = exam.Date,
                ClassAverage = classAverages.TryGetValue(exam.Id, out var average) ? average : 0,
                ClassId = exam.ClassId,
                SchoolId = exam.Class?.SchoolId,
                Grades = exam.Grades.Select(g => g.ToDto()).ToList()
            };
        }

        public static List<ExamDto> ToDto(this List<Exam> exams, IReadOnlyDictionary<Guid, decimal> classAverages)
        {
            return exams.Select(e => e.ToDto(classAverages)).ToList();
        }
    }
}
