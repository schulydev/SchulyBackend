using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.SchoolSystem;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.SchoolSystem;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrator")]
    public class SchoolSystemsController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<SchoolSystemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchoolSystems(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolSystemsQuery(IncludeDisabled: true), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SchoolSystemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchoolSystem(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolSystemQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSchoolSystem([FromBody] CreateSchoolSystemCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSchoolSystem([FromBody] UpdateSchoolSystemCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteSchoolSystem(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteSchoolSystemCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
