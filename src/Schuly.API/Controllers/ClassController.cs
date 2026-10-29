using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Class;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClassController(IMediator mediator) : ControllerBase
    {
        [HttpGet("search")]
        [ProducesResponseType(typeof(ClassDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetClass([FromQuery] Guid classId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetClassQuery(classId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<ClassDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetClasses(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetClassesQuery(), cancellationToken);
            return result.ToActionResult();
        }
    }
}
