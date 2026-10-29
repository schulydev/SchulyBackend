using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> GetSchoolUsers(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolUsersQuery(), cancellationToken);
            return result.ToActionResult();
        }
    }
}
