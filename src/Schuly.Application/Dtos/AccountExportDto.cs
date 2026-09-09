namespace Schuly.Application.Dtos
{
    public class AccountExportDto
    {
        public DateTime ExportedAt { get; set; }
        public required ApplicationUserDto Profile { get; set; }
        public List<SchoolUserDto> SchoolUsers { get; set; } = [];
        public List<AgendaEntryDto> AgendaEntries { get; set; } = [];
        public List<SemesterReportDto> SemesterReports { get; set; } = [];
        public List<StudentDocumentDto> Documents { get; set; } = [];
    }
}
