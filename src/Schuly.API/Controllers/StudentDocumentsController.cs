using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.StudentDocument;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.StudentDocument;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Authorize]
    public class StudentDocumentsController(IMediator mediator) : ControllerBase
    {
        [HttpGet("api/documents")]
        [ProducesResponseType(typeof(List<StudentDocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List([FromQuery] Guid? schoolUserId, CancellationToken ct)
        {
            var result = await mediator.Send(new GetStudentDocumentsQuery(schoolUserId), ct);
            return result.ToActionResult();
        }

        [HttpPost("api/students/{schoolUserId:guid}/documents")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)]   // 50 MB
        public async Task<IActionResult> Upload(Guid schoolUserId, IFormFile file, [FromForm] string title, [FromForm] string? comment, [FromForm] string? category, [FromForm] string? enteredBy, [FromForm] string? followUpAction, [FromForm] DateOnly? followUpDate, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest("Missing file");

            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new UploadStudentDocumentCommand(
                SchoolUserId: schoolUserId,
                Content: stream,
                FileName: file.FileName,
                ContentType: file.ContentType,
                Title: title,
                Comment: comment,
                Category: category,
                EnteredBy: enteredBy,
                FollowUpAction: followUpAction,
                FollowUpDate: followUpDate), ct);

            return result.IsSuccess ? Ok(new { id = result.Value }) : BadRequest(result.Error);
        }

        /// <summary>
        /// Streams the file bytes through the backend — no signed URLs, no
        /// direct S3 access from clients. Every byte hits the same auth gate
        /// as the rest of the API.
        /// </summary>
        [HttpGet("api/documents/{documentId:guid}")]
        public async Task<IActionResult> Download(Guid documentId, CancellationToken ct)
        {
            var result = await mediator.Send(new OpenStudentDocumentQuery(documentId), ct);
            if (!result.IsSuccess) return BadRequest(result.Error);

            var download = result.Value!;
            return File(
                download.Stream.Content,
                download.Stream.ContentType ?? "application/octet-stream",
                download.FileName);
        }
    }
}
