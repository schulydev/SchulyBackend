using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class SemesterReportMapper
    {
        public static SemesterSubjectGradeDto ToDto(this SemesterSubjectGrade subject)
        {
            return new SemesterSubjectGradeDto
            {
                Id = subject.Id,
                SemesterReportId = subject.SemesterReportId,
                SubjectCode = subject.SubjectCode,
                SubjectName = subject.SubjectName,
                SubjectTypeMarker = subject.SubjectTypeMarker,
                Grade = subject.Grade,
                Marker = subject.Marker
            };
        }

        public static SemesterReportDto ToDto(this SemesterReport report)
        {
            return new SemesterReportDto
            {
                Id = report.Id,
                SchoolUserId = report.SchoolUserId,
                ProgramCode = report.ProgramCode,
                SchoolYearStart = report.SchoolYearStart,
                SemesterHalf = report.SemesterHalf,
                ClassName = report.ClassName,
                PromotionDecision = report.PromotionDecision,
                GradeAverage = report.GradeAverage,
                InsufficientGradeCount = report.InsufficientGradeCount,
                DeficiencyPoints = report.DeficiencyPoints,
                ExcusedAbsences = report.ExcusedAbsences,
                UnexcusedAbsences = report.UnexcusedAbsences,
                TotalAbsences = report.TotalAbsences,
                Subjects = report.Subjects.Select(s => s.ToDto()).ToList()
            };
        }

        public static List<SemesterReportDto> ToDto(this List<SemesterReport> reports)
        {
            return reports.Select(r => r.ToDto()).ToList();
        }
    }
}
