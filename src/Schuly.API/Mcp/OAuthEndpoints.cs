using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Schuly.Application.Queries.Mcp;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    /// <summary>
    /// The anonymous half of the authorization server: metadata (RFC 8414), dynamic client registration (RFC 7591), the authorization redirect, the token
    /// endpoint and revocation (RFC 7009). Consent itself happens in the web app at <c>/connect/authorize</c>, which uses the user's normal signed-in session.
    /// </summary>
    public static class OAuthEndpoints
    {
        public const string ConsentPath = "/connect/authorize";
        public const string RegistrationRateLimit = "mcp-oauth-register";
        public const string TokenRateLimit = "mcp-oauth-token";

        public static IEndpointRouteBuilder MapSchulyOAuthEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/.well-known/oauth-authorization-server", Metadata).AllowAnonymous().ExcludeFromDescription();

            var oauth = app.MapGroup("/oauth").AllowAnonymous().ExcludeFromDescription().WithHttpLogging(Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.None);
            oauth.MapPost("/register", RegisterAsync).RequireRateLimiting(RegistrationRateLimit);
            oauth.MapGet("/authorize", AuthorizeAsync);
            oauth.MapPost("/token", TokenAsync).DisableAntiforgery().RequireRateLimiting(TokenRateLimit);
            oauth.MapPost("/revoke", RevokeAsync).DisableAntiforgery().RequireRateLimiting(TokenRateLimit);
            return app;
        }

        private static IResult Metadata(McpPublicUrls urls) => NoStore(Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = urls.Issuer,
            ["authorization_endpoint"] = urls.Issuer + "/oauth/authorize",
            ["token_endpoint"] = urls.Issuer + "/oauth/token",
            ["registration_endpoint"] = urls.Issuer + "/oauth/register",
            ["revocation_endpoint"] = urls.Issuer + "/oauth/revoke",
            ["response_types_supported"] = McpOAuthService.ResponseTypes,
            ["response_modes_supported"] = new[] { "query" },
            ["grant_types_supported"] = McpOAuthService.GrantTypes,
            ["code_challenge_methods_supported"] = new[] { Pkce.Method },
            ["token_endpoint_auth_methods_supported"] = new[] { McpOAuthService.AuthMethodNone },
            ["revocation_endpoint_auth_methods_supported"] = new[] { McpOAuthService.AuthMethodNone },
            ["scopes_supported"] = McpScopes.Supported,
            ["authorization_response_iss_parameter_supported"] = true,
            ["service_documentation"] = urls.Issuer + "/ai/mcp",
        }));

        private static async Task<IResult> RegisterAsync(HttpContext context, McpOAuthService oauth)
        {
            ClientRegistrationRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>(context.RequestAborted);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or BadHttpRequestException)
            {
                return Error("invalid_client_metadata", "The body must be a JSON client metadata document.");
            }
            if (request is null)
                return Error("invalid_client_metadata", "The body must be a JSON client metadata document.");

            var (response, error) = await oauth.RegisterClientAsync(request, context.Connection.RemoteIpAddress?.ToString(), context.RequestAborted);
            return error is not null
                ? Error(error.Error, error.Description, error.Error == "temporarily_unavailable" ? StatusCodes.Status429TooManyRequests : StatusCodes.Status400BadRequest)
                : NoStore(Results.Json(response, statusCode: StatusCodes.Status201Created));
        }

        /// <summary>
        /// Never shows anything itself. A request whose client or redirect URI cannot be trusted goes to the consent page, which explains the problem; an error the
        /// client should hear about goes back to its registered redirect URI; a valid request goes to the consent page, where the user signs in if needed.
        /// </summary>
        private static async Task<IResult> AuthorizeAsync(HttpContext context, McpOAuthService oauth, McpPublicUrls urls)
        {
            var q = context.Request.Query;
            var request = new AuthorizationRequest(q["response_type"], q["client_id"], q["redirect_uri"], q["scope"], q["state"], q["code_challenge"], q["code_challenge_method"], q["resource"]);
            var check = await oauth.CheckAuthorizationRequestAsync(request, urls.Resource, context.RequestAborted);
            if (check.Status == AuthorizationRequestStatus.Redirect)
                return Results.Redirect(McpRedirects.Error(check.RedirectUri!, check.Error!, check.ErrorDescription, request.State, urls.Issuer));

            return Results.Redirect(ConsentPath + context.Request.QueryString.Value);
        }

        private static async Task<IResult> TokenAsync(HttpContext context, McpOAuthService oauth, McpPublicUrls urls)
        {
            if (!context.Request.HasFormContentType)
                return Error("invalid_request", "Send the token request as application/x-www-form-urlencoded.");

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!string.IsNullOrEmpty(form["client_secret"]) || !string.IsNullOrEmpty(context.Request.Headers.Authorization))
                return Error("invalid_client", "Clients of this server are public: send client_id only, no client secret.", StatusCodes.Status401Unauthorized);

            var grantType = form["grant_type"].ToString();
            var (response, error) = grantType switch
            {
                "authorization_code" => await oauth.ExchangeCodeAsync(form["client_id"], form["code"], form["redirect_uri"], form["code_verifier"], NullIfEmpty(form["resource"]), urls.Resource, context.RequestAborted),
                "refresh_token" => await oauth.RefreshAsync(form["client_id"], form["refresh_token"], NullIfEmpty(form["resource"]), NullIfEmpty(form["scope"]), urls.Resource, context.RequestAborted),
                _ => (null, new OAuthError("unsupported_grant_type", "Supported grant types are authorization_code and refresh_token.")),
            };

            return error is not null ? Error(error.Error, error.Description) : NoStore(Results.Json(response));
        }

        private static async Task<IResult> RevokeAsync(HttpContext context, McpOAuthService oauth)
        {
            if (!context.Request.HasFormContentType)
                return Error("invalid_request", "Send the revocation request as application/x-www-form-urlencoded.");
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            await oauth.RevokeTokenAsync(form["token"], NullIfEmpty(form["client_id"]), context.RequestAborted);
            return NoStore(Results.Ok());
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

        private static IResult Error(string error, string description, int status = StatusCodes.Status400BadRequest) =>
            NoStore(Results.Json(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }, statusCode: status));

        private static IResult NoStore(IResult inner) => new NoStoreResult(inner);

        /// <summary>RFC 6749 section 5.1: token responses (and, for simplicity, every response here) must not be cached.</summary>
        private sealed class NoStoreResult(IResult inner) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.Headers.CacheControl = "no-store";
                httpContext.Response.Headers.Pragma = "no-cache";
                return inner.ExecuteAsync(httpContext);
            }
        }
    }
}
