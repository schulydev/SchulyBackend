using System.Text;
using System.Text.Json;

namespace Schuly.API.Mcp
{
    /// <summary>The answer of a plugin endpoint called in process.</summary>
    public sealed record PluginApiResponse(int StatusCode, string? ContentType, byte[] Body)
    {
        public bool IsSuccess => StatusCode is >= 200 and < 300;

        public JsonElement? Json()
        {
            if (Body.Length == 0 || ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
                return null;
            try
            {
                using var doc = JsonDocument.Parse(Body);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public string Text() => Encoding.UTF8.GetString(Body);
    }

    /// <summary>
    /// Calls plugin endpoints (<c>/api/plugins/...</c>) without leaving the process and without minting a credential. Plugins load into their own
    /// AssemblyLoadContext and DI container, so the host cannot reference their services; this runs the request through the app's own routing, authorization
    /// and plugin-scope middleware instead, with the MCP caller's principal. The plugin therefore sees exactly the user, and applies exactly the checks, of a REST call.
    /// </summary>
    public sealed class InProcessPluginApi(IHttpContextAccessor httpContextAccessor, IServiceScopeFactory scopes, ILogger<InProcessPluginApi> logger)
    {
        private const int MaxResponseBytes = 8 * 1024 * 1024;
        private static RequestDelegate? _pipeline;

        /// <summary>Builds the branch once at startup: the same routing, authorization and plugin scope as the main pipeline, minus everything that faces the network.</summary>
        public static void Build(WebApplication app)
        {
            var branch = ((IApplicationBuilder)app).New();
            // UseRouting only matches the app's endpoints (controllers, plugin data sources) when it is handed the app's route builder; otherwise it starts an empty one.
            branch.Properties["__GlobalEndpointRouteBuilder"] = app;
            branch.UseRouting();
            branch.UseAuthorization();
            branch.UseMiddleware<Plugins.PluginScopeMiddleware>();
            branch.UseEndpoints(_ => { });
            _pipeline = branch.Build();
        }

        public Task<PluginApiResponse> GetAsync(string pathAndQuery, CancellationToken ct) => SendAsync(HttpMethods.Get, pathAndQuery, null, ct);

        public Task<PluginApiResponse> PostAsync(string pathAndQuery, object? body, CancellationToken ct) => SendAsync(HttpMethods.Post, pathAndQuery, body, ct);

        public Task<PluginApiResponse> DeleteAsync(string pathAndQuery, CancellationToken ct) => SendAsync(HttpMethods.Delete, pathAndQuery, null, ct);

        public async Task<PluginApiResponse> SendAsync(string method, string pathAndQuery, object? body, CancellationToken ct)
        {
            if (!pathAndQuery.StartsWith("/api/plugins/", StringComparison.Ordinal))
                throw new ArgumentException("Only plugin endpoints can be called in process.", nameof(pathAndQuery));
            var pipeline = _pipeline ?? throw new InvalidOperationException("The in-process plugin pipeline was not built.");
            var outer = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Plugin calls need the MCP request they run in.");

            await using var scope = scopes.CreateAsyncScope();
            var context = new DefaultHttpContext();
            context.RequestServices = scope.ServiceProvider;
            context.User = outer.User;
            context.RequestAborted = ct;
            context.Connection.RemoteIpAddress = outer.Connection.RemoteIpAddress;

            var queryIndex = pathAndQuery.IndexOf('?');
            var request = context.Request;
            request.Method = method;
            request.Scheme = outer.Request.Scheme;
            request.Host = outer.Request.Host;
            request.Path = queryIndex < 0 ? pathAndQuery : pathAndQuery[..queryIndex];
            request.QueryString = queryIndex < 0 ? QueryString.Empty : new QueryString(pathAndQuery[queryIndex..]);
            if (body is not null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web);
                request.Body = new MemoryStream(bytes);
                request.ContentLength = bytes.Length;
                request.ContentType = "application/json";
            }
            else if (method != HttpMethods.Get && method != HttpMethods.Delete)
            {
                request.ContentLength = 0;
            }

            var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await pipeline(context);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // The path is logged without its query, which can carry search text.
                logger.LogWarning(ex, "In-process plugin call {Method} {Path} failed", method, request.Path.Value);
                return new PluginApiResponse(StatusCodes.Status502BadGateway, "text/plain", []);
            }

            if (context.Response.StatusCode >= 500)
                logger.LogWarning("In-process plugin call {Method} {Path} answered {Status}", method, request.Path.Value, context.Response.StatusCode);

            if (responseBody.Length > MaxResponseBytes)
                return new PluginApiResponse(StatusCodes.Status502BadGateway, "text/plain", Encoding.UTF8.GetBytes("The plugin answer was too large."));
            return new PluginApiResponse(context.Response.StatusCode, context.Response.ContentType, responseBody.ToArray());
        }
    }
}
