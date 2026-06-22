using Mediator;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.User;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.User;
using Schuly.Plugin.Abstractions;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IMediator mediator, IEnumerable<IPluginLogin> logins) : ControllerBase
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

        /// <summary>
        /// Unified plugin login. The CRM is dumb: it resolves the
        /// <see cref="IPluginLogin"/> whose <c>SystemKey</c> matches and forwards
        /// the catalog-collected <c>fields</c> to it. The plugin authenticates its
        /// provider, stores the account, and returns its id. One endpoint for every
        /// system (Schulnetz email+password, OdAOrg username+password, …).
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login(
            [FromBody] UnifiedLoginRequest request, CancellationToken cancellationToken)
        {
            var login = logins.FirstOrDefault(
                l => string.Equals(l.SystemKey, request.SystemKey, StringComparison.OrdinalIgnoreCase));
            if (login is null)
                return BadRequest(new { message = $"No plugin handles system '{request.SystemKey}'." });

            var result = await login.ConnectAsync(
                request.Fields ?? new Dictionary<string, string>(), request.DisplayName, cancellationToken);

            return result.Success
                ? Ok(new { accountId = result.AccountId, message = result.Message })
                : BadRequest(new { message = result.Message });
        }
    }

    /// <summary>Body for the unified plugin login endpoint.</summary>
    public record UnifiedLoginRequest(
        string SystemKey,
        Dictionary<string, string>? Fields,
        string? DisplayName);
}
