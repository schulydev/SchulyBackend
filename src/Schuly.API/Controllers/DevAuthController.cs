using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Schuly.API.Extensions;
using System.Security.Claims;

namespace Schuly.API.Controllers
{
    /// <summary>
    /// Development-only fake OIDC. Mints JWTs the backend trusts so authenticated and
    /// role-gated endpoints can be tested without a real IdP. Active only when
    /// <c>DevAuth:Enabled</c> is set (Development); returns 404 otherwise.
    /// </summary>
    [ApiController]
    [Route("api/dev")]
    [AllowAnonymous]
    public class DevAuthController(IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
    {
        public record DevTokenRequest(string? Role = "Administrator", string? Sub = "dev-admin", string? Name = "Dev Admin", string? Email = null);

        public record DevTokenResponse(string AccessToken, string TokenType, int ExpiresIn, string Role);

        [HttpPost("token")]
        [ProducesResponseType(typeof(DevTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Token([FromBody] DevTokenRequest? request)
        {
            if (!DevAuthDefaults.IsEnabled(configuration, environment))
                return NotFound();

            var role = string.IsNullOrWhiteSpace(request?.Role) ? "Administrator" : request!.Role!;
            var sub = string.IsNullOrWhiteSpace(request?.Sub) ? "dev-admin" : request!.Sub!;
            var name = string.IsNullOrWhiteSpace(request?.Name) ? "Dev Admin" : request!.Name!;
            var email = string.IsNullOrWhiteSpace(request?.Email) ? $"{sub}@schuly.dev" : request!.Email!;

            var now = DateTime.UtcNow;
            const int lifetimeSeconds = 12 * 60 * 60;

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = DevAuthDefaults.Issuer(configuration),
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddSeconds(lifetimeSeconds),
                SigningCredentials = new SigningCredentials(
                    DevAuthDefaults.SigningKey(configuration), SecurityAlgorithms.HmacSha256),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = sub,
                    ["name"] = name,
                    [ClaimTypes.Name] = name,
                    [ClaimTypes.Email] = email,
                    [ClaimTypes.Role] = role,
                    ["groups"] = role,
                }
            };

            var token = new JsonWebTokenHandler().CreateToken(descriptor);
            return Ok(new DevTokenResponse(token, "Bearer", lifetimeSeconds, role));
        }
    }
}
