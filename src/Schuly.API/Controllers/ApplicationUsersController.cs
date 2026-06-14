using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.ApplicationUser;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.ApplicationUser;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApplicationUsersController(IMediator mediator) : ControllerBase
    {
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApplicationUserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplicationUser(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetApplicationUserQuery(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ApplicationUserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetApplicationUsers(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetApplicationUsersQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateApplicationUser([FromBody] CreateApplicationUserCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateApplicationUser([FromBody] UpdateApplicationUserCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteApplicationUser(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteApplicationUserCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
