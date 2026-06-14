using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.SchoolUser;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.SchoolUser;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SchoolUsersController(IMediator mediator) : ControllerBase
    {
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SchoolUserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchoolUser(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolUserQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<SchoolUserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchoolUsers([FromQuery] Guid? applicationUserId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolUsersQuery(applicationUserId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateSchoolUser([FromBody] CreateSchoolUserCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSchoolUser([FromBody] UpdateSchoolUserCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteSchoolUser(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteSchoolUserCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
