using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.App;
using Schuly.Application.Queries.SchoolSystem;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppController(IMediator mediator) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(AppDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new AppQuery(), cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>The catalog of enabled school systems the app renders for login.</summary>
        [AllowAnonymous]
        [HttpGet("school-systems")]
        [ProducesResponseType(typeof(List<SchoolSystemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSchoolSystems(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetSchoolSystemsQuery(), cancellationToken);
            return result.ToActionResult();
        }
    }
}
