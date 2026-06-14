using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.SemesterReport;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SemesterReportsController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<SemesterReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSemesterReports([FromQuery] Guid? schoolUserId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSemesterReportsQuery(schoolUserId), cancellationToken);
            return result.ToActionResult();
        }
    }
}
