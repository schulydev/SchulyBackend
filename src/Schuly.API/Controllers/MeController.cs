using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.Me;
using Schuly.Application.Commands.User;

namespace Schuly.API.Controllers
{
    /// <summary>Hand-entered data of the signed-in user, for using Schuly without a provider plugin.</summary>
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController(IMediator mediator) : ControllerBase
    {
        [HttpPut("avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(SetMyAvatarCommandHandler.MaxBytes + 100_000)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SetAvatar(IFormFile file, CancellationToken cancellationToken)
        {
            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new SetMyAvatarCommand(stream, file.ContentType, file.Length), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("avatar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new RemoveMyAvatarCommand(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("schools")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMySchool([FromBody] CreateMySchoolCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("grades")]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddMyGrade([FromBody] AddMyGradeCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("grades")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMyGrade([FromBody] UpdateMyGradeCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("grades/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteMyGrade(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteMyGradeCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
