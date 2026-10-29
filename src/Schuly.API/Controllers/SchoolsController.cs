using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.School;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SchoolsController(IMediator mediator) : ControllerBase
    {
        [HttpGet("my-schools")]
        [ProducesResponseType(typeof(List<MySchoolDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetMySchools(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMySchoolsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SchoolDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchool(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolQuery(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
