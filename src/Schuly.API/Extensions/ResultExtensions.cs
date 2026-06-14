using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Models;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Maps a <see cref="Result"/> / <see cref="Result{T}"/> to the HTTP response
    /// every controller action used to spell out by hand: success returns the
    /// value (or 204 for a valueless result), failure returns 400 with the error.
    /// One place owns that mapping.
    /// </summary>
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(this Result<T> result) =>
            result.IsSuccess
                ? new OkObjectResult(result.Value)
                : new BadRequestObjectResult(result.Error);

        public static IActionResult ToActionResult(this Result result) =>
            result.IsSuccess
                ? new NoContentResult()
                : new BadRequestObjectResult(result.Error);
    }
}
