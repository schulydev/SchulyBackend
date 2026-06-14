using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Models;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Maps a <see cref="Result"/> / <see cref="Result{T}"/> to the HTTP response
    /// every controller action used to spell out by hand: success returns the
    /// value (or 204 for a valueless result), a forbidden result returns 403, a
    /// conflict returns 409, and any other failure returns 400 with the error.
    /// One place owns that mapping.
    /// </summary>
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(this Result<T> result) =>
            result.IsSuccess ? new OkObjectResult(result.Value) : Error(result.Status, result.Error);

        public static IActionResult ToActionResult(this Result result) =>
            result.IsSuccess ? new NoContentResult() : Error(result.Status, result.Error);

        private static IActionResult Error(ResultStatus status, string? error) => status switch
        {
            ResultStatus.Forbidden => new ObjectResult(error) { StatusCode = StatusCodes.Status403Forbidden },
            ResultStatus.Conflict => new ObjectResult(error) { StatusCode = StatusCodes.Status409Conflict },
            _ => new BadRequestObjectResult(error),
        };
    }
}
