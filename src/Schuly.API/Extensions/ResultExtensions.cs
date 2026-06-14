using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Schuly.Application.Models;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Maps a <see cref="Result"/> / <see cref="Result{T}"/> to the HTTP response
    /// every controller action used to spell out by hand: success returns the
    /// value (or 204 for a valueless result), a forbidden result returns 403, and
    /// any other failure returns 400 with the error. One place owns that mapping.
    /// </summary>
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(this Result<T> result) =>
            result.IsSuccess ? new OkObjectResult(result.Value) : Error(result.IsForbidden, result.Error);

        public static IActionResult ToActionResult(this Result result) =>
            result.IsSuccess ? new NoContentResult() : Error(result.IsForbidden, result.Error);

        private static IActionResult Error(bool isForbidden, string? error) =>
            isForbidden
                ? new ObjectResult(error) { StatusCode = StatusCodes.Status403Forbidden }
                : new BadRequestObjectResult(error);
    }
}
