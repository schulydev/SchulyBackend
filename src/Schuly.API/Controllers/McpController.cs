using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.Mcp;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Mcp;
using Schuly.Domain.Mcp;

namespace Schuly.API.Controllers
{
    /// <summary>The signed-in user's side of the MCP server: the consent decision, connected apps and API keys. Uses the normal web session, never an MCP token.</summary>
    [ApiController]
    [Route("api/mcp")]
    [Authorize]
    [HttpLogging(HttpLoggingFields.None)]
    public class McpController(IMediator mediator) : ControllerBase
    {
        [HttpPost("authorization-request")]
        [ProducesResponseType(typeof(McpAuthorizationRequestDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckAuthorizationRequest([FromBody] McpAuthorizationParameters parameters, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMcpAuthorizationRequestQuery(parameters), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("authorization-decision")]
        [ProducesResponseType(typeof(McpAuthorizationDecisionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DecideAuthorization([FromBody] DecideMcpAuthorizationCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("connections")]
        [ProducesResponseType(typeof(List<McpConnectionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMcpConnectionsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut("connections/{id:guid}/permissions")]
        [ProducesResponseType(typeof(McpConnectionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateConnectionPermissions(Guid id, [FromBody] McpPermissionSet permissions, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new UpdateMcpConnectionPermissionsCommand(id, permissions), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("connections/{id:guid}/activity")]
        [ProducesResponseType(typeof(List<McpActivityDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConnectionActivity(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMcpActivityQuery(id, null), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("connections/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeConnection(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new RevokeMcpConnectionCommand(id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("keys")]
        [ProducesResponseType(typeof(List<McpApiKeyDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApiKeys(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMcpApiKeysQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost("keys")]
        [ProducesResponseType(typeof(CreatedMcpApiKeyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateApiKey([FromBody] CreateMcpApiKeyCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return result.ToActionResult();
        }

        [HttpPut("keys/{id:guid}/permissions")]
        [ProducesResponseType(typeof(McpApiKeyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateApiKeyPermissions(Guid id, [FromBody] McpPermissionSet permissions, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new UpdateMcpApiKeyPermissionsCommand(id, permissions), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("keys/{id:guid}/activity")]
        [ProducesResponseType(typeof(List<McpActivityDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApiKeyActivity(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetMcpActivityQuery(null, id), cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("keys/{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new RevokeMcpApiKeyCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
