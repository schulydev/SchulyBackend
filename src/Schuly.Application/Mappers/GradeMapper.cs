using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class GradeMapper
    {
        public static GradeDto ToDto(this Grade grade)
        {
            return new GradeDto
            {
                Id = grade.Id,
                Score = grade.Score,
                Weighting = grade.Weighting,
                ExamId = grade.ExamId,
                SchoolUserId = grade.SchoolUserId
            };
        }

        public static List<GradeDto> ToDto(this List<Grade> grades)
        {
            return grades.Select(g => g.ToDto()).ToList();
        }
    }
}
