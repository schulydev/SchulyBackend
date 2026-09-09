using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.Notification;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Notification;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController(IMediator mediator) : ControllerBase
    {
        [HttpPost("devices")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceTokenCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("devices/{token}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveDevice(string token, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new RemoveDeviceTokenCommand(token), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("preferences")]
        [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetNotificationPreferencesQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("preferences")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }
    }
}
