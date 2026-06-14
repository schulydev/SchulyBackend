using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.School;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.School;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SchoolsController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<SchoolDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchools(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolsQuery(), cancellationToken);
            return result.ToActionResult();
        }

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

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSchool([FromBody] UpdateSchoolCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteSchool(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteSchoolCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
