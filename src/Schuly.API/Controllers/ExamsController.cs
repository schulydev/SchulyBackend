using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Exam;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExamsController(IMediator mediator) : ControllerBase
    {
        [HttpGet("search")]
        [ProducesResponseType(typeof(ExamDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetExam([FromQuery] Guid examId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetExamQuery(examId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ExamDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetExams(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetExamsQuery(), cancellationToken);
            return result.ToActionResult();
        }
    }
}
