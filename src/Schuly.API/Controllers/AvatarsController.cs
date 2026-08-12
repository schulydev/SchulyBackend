using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Queries.Avatar;
using Schuly.Infrastructure.Services;

namespace Schuly.API.Controllers
{
    [ApiController]
    [Route("api/avatars")]
    public class AvatarsController(IMediator mediator, IAvatarUrlSigner signer) : ControllerBase
    {
        [HttpGet("{schoolUserId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(Guid schoolUserId, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(sig) || !signer.Verify(schoolUserId, exp, sig))
                return NotFound();

            var result = await mediator.Send(new GetAvatarQuery(schoolUserId), ct);
            if (!result.IsSuccess) return NotFound();

            var stream = result.Value!.Stream;
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(stream.Content, stream.ContentType ?? "image/png");
        }
    }
}
