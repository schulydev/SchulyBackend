using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.API.Plugins;
using Schuly.API.Services;
using Schuly.Application.Dtos;
using Schuly.Application.Queries.Plugins;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrator")]
    public class PluginsController(IMediator mediator, PluginSchedulerRegistry scheduler, PluginManager plugins) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(List<PluginDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new GetPluginsQuery(), cancellationToken);
            return result.ToActionResult();
        }

        [HttpGet("scheduler")]
        [ProducesResponseType(typeof(IReadOnlyList<PluginTaskStatus>), StatusCodes.Status200OK)]
        public IActionResult Scheduler() => Ok(scheduler.Snapshot());

        [HttpGet("registry")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Registry(CancellationToken cancellationToken) =>
            Ok(await plugins.GetRegistryAsync(cancellationToken));

        public record InstallPluginRequest(string Name, string? Version);

        [HttpPost("install")]
        [Authorize(Roles = "Administrator")]
        public Task<IActionResult> Install([FromBody] InstallPluginRequest request, CancellationToken cancellationToken) =>
            ApplyAsync(() => plugins.InstallAsync(request.Name, request.Version, cancellationToken), request.Name);

        [HttpPost("{name}/update")]
        [Authorize(Roles = "Administrator")]
        public Task<IActionResult> Update(string name, CancellationToken cancellationToken) =>
            ApplyAsync(() => plugins.UpdateAsync(name, cancellationToken), name);

        [HttpDelete("{name}")]
        [Authorize(Roles = "Administrator")]
        public Task<IActionResult> Remove(string name, CancellationToken cancellationToken) =>
            ApplyAsync(() => plugins.RemoveAsync(name, cancellationToken), name);

        private async Task<IActionResult> ApplyAsync(Func<Task> action, string name)
        {
            try
            {
                await action();
                return Ok(new { plugin = name, loaded = plugins.Loaded() });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
