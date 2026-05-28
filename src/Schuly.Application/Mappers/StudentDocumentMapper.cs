using Schuly.Application.Dtos;
using Schuly.Domain;

namespace Schuly.Application.Mappers
{
    public static class StudentDocumentMapper
    {
        public static StudentDocumentDto ToDto(this StudentDocument document)
        {
            return new StudentDocumentDto
            {
                Id = document.Id,
                SchoolUserId = document.SchoolUserId,
                Title = document.Title,
                Comment = document.Comment,
                Category = document.Category,
                EnteredBy = document.EnteredBy,
                FileName = document.FileName,
                FileSizeBytes = document.FileSizeBytes,
                FollowUpAction = document.FollowUpAction,
                FollowUpDate = document.FollowUpDate,
                CompletedDate = document.CompletedDate,
                NotifiedAt = document.NotifiedAt,
                CreatedAt = document.CreatedAt
            };
        }

        public static List<StudentDocumentDto> ToDto(this List<StudentDocument> documents)
        {
            return documents.Select(d => d.ToDto()).ToList();
        }
    }
}
