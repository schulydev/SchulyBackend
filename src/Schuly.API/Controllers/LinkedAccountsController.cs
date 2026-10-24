using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.LinkedAccount;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.LinkedAccount;

namespace Schuly.API.Controllers
{
    /// <summary>Accounts the signed-in user linked with an outside identity provider (e.g. Microsoft).</summary>
    [ApiController]
    [Route("api/me/linked-accounts")]
    [Authorize]
    public class LinkedAccountsController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<LinkedAccountDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyLinkedAccounts([FromQuery] string? provider, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMyLinkedAccountsQuery(provider), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RemoveMyLinkedAccount(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new RemoveMyLinkedAccountCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
