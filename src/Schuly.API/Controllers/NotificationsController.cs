using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Schuly.API.Services.Notifications;
using Schuly.Application.Commands.Notification;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Notification;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController(IMediator mediator, SchulyDbContext db, IUserService userService, IPushSender pushSender) : ControllerBase
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

        /// <summary>Sends a test notification to the signed-in user's own registered devices.</summary>
        [HttpPost("test")]
        [ProducesResponseType(typeof(PushResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SendTest(CancellationToken cancellationToken)
        {
            if (!pushSender.IsConfigured)
                return Conflict(new { message = "Push notifications are not configured on this server." });

            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var tokens = await db.DeviceTokens.Where(d => d.ApplicationUserId == userId).Select(d => d.Token).ToListAsync(cancellationToken);
            if (tokens.Count == 0)
                return Conflict(new { message = "No device is registered for notifications yet. Turn them on in the app first." });

            var result = await pushSender.SendAsync(tokens, new PushMessage("Schuly", "Notifications work on this device.", new Dictionary<string, string> { ["type"] = "test" }), cancellationToken);
            if (result.UnregisteredTokens.Count > 0)
            {
                await db.DeviceTokens.Where(d => d.ApplicationUserId == userId && result.UnregisteredTokens.Contains(d.Token)).ExecuteDeleteAsync(cancellationToken);
            }
            return Ok(result);
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
