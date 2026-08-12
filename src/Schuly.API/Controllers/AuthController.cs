using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.User;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.User;
using Schuly.API.Plugins;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AuthController(IMediator mediator, PluginHost pluginHost) : ControllerBase
    {
        [HttpGet("me")]
        [ProducesResponseType(typeof(ApplicationUserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("sync")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Sync(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SyncUserCommand(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] UnifiedLoginRequest request, CancellationToken cancellationToken)
        {
            var result = await pluginHost.ConnectAsync(
                request.SystemKey, request.Fields ?? new Dictionary<string, string>(), request.DisplayName, cancellationToken);
            if (result is null)
                return BadRequest(new { message = $"No plugin handles system '{request.SystemKey}'." });

            return result.Success
                ? Ok(new { accountId = result.AccountId, message = result.Message })
                : BadRequest(new { message = result.Message });
        }
    }

    public record UnifiedLoginRequest(string SystemKey, Dictionary<string, string>? Fields, string? DisplayName);
}
