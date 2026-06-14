using Mediator;
using Microsoft.AspNetCore.Mvc;
using Schuly.API.Services;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Plugins;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PluginsController(IMediator mediator, PluginSchedulerRegistry scheduler) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<PluginDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPluginsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Aggregated background-task scheduler health: last run, success/failure,
        /// duration, error, and consecutive failures per plugin sync task. Live
        /// runtime state (resets on restart). Per-account detail lives on each
        /// plugin's own .../accounts/{id}/sync endpoint.
        /// </summary>
        [HttpGet("scheduler")]
        [ProducesResponseType(typeof(IReadOnlyList<PluginTaskStatus>), StatusCodes.Status200OK)]
        public IActionResult Scheduler() => Ok(scheduler.Snapshot());
    }
}
