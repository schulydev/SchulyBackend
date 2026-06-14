using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Commands.Agenda;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Agenda;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AgendasController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<AgendaEntryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAgendas(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetAgendasQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("search")]
        [ProducesResponseType(typeof(AgendaEntryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAgenda([FromQuery] Guid agendaEntryId, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetAgendaQuery(agendaEntryId), cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEntry([FromBody] CreateAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPut]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateEntry([FromBody] UpdateAgendaEntryCommand command, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(command, cancellationToken);
            return result.ToActionResult();
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new DeleteAgendaEntryCommand(id), cancellationToken);
            return result.ToActionResult();
        }
    }
}
