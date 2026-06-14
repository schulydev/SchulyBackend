using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Schuly.API.Extensions;
using Schuly.Application.Models;

namespace Schuly.Tests
{
    public class ForbiddenStatusTests
    {
        [Test]
        public async Task Forbidden_result_maps_to_403()
        {
            var action = Result.Forbidden().ToActionResult();

            var obj = action as ObjectResult;
            await Assert.That(obj).IsNotNull();
            await Assert.That(obj!.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
        }

        [Test]
        public async Task Forbidden_generic_result_maps_to_403()
        {
            var action = Result<string>.Forbidden().ToActionResult();

            var obj = action as ObjectResult;
            await Assert.That(obj).IsNotNull();
            await Assert.That(obj!.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
        }

        [Test]
        public async Task Conflict_result_maps_to_409()
        {
            var action = Result.Conflict("has dependents").ToActionResult();

            var obj = action as ObjectResult;
            await Assert.That(obj).IsNotNull();
            await Assert.That(obj!.StatusCode).IsEqualTo(StatusCodes.Status409Conflict);
        }

        [Test]
        public async Task Plain_failure_still_maps_to_400()
        {
            var action = Result.Failure("nope").ToActionResult();

            await Assert.That(action as BadRequestObjectResult).IsNotNull();
        }

        [Test]
        public async Task UnauthorizedAccessException_is_handled_as_403()
        {
            var handler = new UnauthorizedExceptionHandler();
            var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            var handled = await handler.TryHandleAsync(ctx, new UnauthorizedAccessException("no"), CancellationToken.None);

            await Assert.That(handled).IsTrue();
            await Assert.That(ctx.Response.StatusCode).IsEqualTo(StatusCodes.Status403Forbidden);
        }

        [Test]
        public async Task Other_exceptions_are_left_to_the_default_handler()
        {
            var handler = new UnauthorizedExceptionHandler();
            var ctx = new DefaultHttpContext();

            var handled = await handler.TryHandleAsync(ctx, new InvalidOperationException(), CancellationToken.None);

            await Assert.That(handled).IsFalse();
        }
    }
}
