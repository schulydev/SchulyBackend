using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Schuly.API.Mcp.Tools;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    public static class McpServerExtensions
    {
        public const string RateLimitPolicy = "mcp";
        public const string CorsPolicy = "SchulyMcpOAuth";
        public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";

        public static IServiceCollection AddSchulyMcp(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<McpOAuthOptions>(configuration.GetSection(McpOAuthOptions.SectionName));
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<McpPublicUrls>();
            services.AddScoped<McpOAuthService>();
            services.AddScoped<McpApiKeyService>();
            services.AddScoped<McpActivityLog>();
            services.AddScoped<InProcessPluginApi>();
            services.AddScoped<McpInternalTokens>();
            services.AddSingleton<McpAuthFailureTracker>();
            services.AddHostedService<McpCleanupService>();

            // Browser-based MCP clients (the MCP Inspector, web IDEs) fetch the metadata and call register/token from their own origin. Only origins listed in
            // Mcp:AllowedOrigins get CORS, never a wildcard, and never with credentials: none of these endpoints reads a cookie.
            var allowedOrigins = configuration.GetSection(McpOAuthOptions.SectionName).Get<McpOAuthOptions>()?.AllowedOrigins.ToArray() ?? [];
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "DELETE")
                .WithHeaders("Authorization", "Content-Type", "MCP-Protocol-Version", "Mcp-Session-Id", "Last-Event-ID")
                .WithExposedHeaders("WWW-Authenticate", "Mcp-Session-Id")));

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, McpTokenAuthenticationHandler>(McpTokenAuthenticationHandler.SchemeName, null)
                // The SDK's scheme only challenges: on a 401 it adds WWW-Authenticate with resource_metadata, and it serves the protected resource metadata.
                // Authentication itself is forwarded to the opaque-token handler above.
                .AddMcp(options =>
                {
                    options.ForwardAuthenticate = McpTokenAuthenticationHandler.SchemeName;
                    options.ResourceMetadataUri = new Uri(ProtectedResourceMetadataPath, UriKind.Relative);
                    options.Events.OnResourceMetadataRequest = context =>
                    {
                        context.ResourceMetadata = ResourceMetadata(context.HttpContext.RequestServices.GetRequiredService<McpPublicUrls>());
                        return Task.CompletedTask;
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(McpPolicies.Endpoint, policy => policy.AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireClaim("sub"));
                McpPolicies.AddScopePolicies(options);
            });

            services.AddRateLimiter(options =>
            {
                // Per connection or key (the credential's hash), and per user across all of them, so one runaway agent cannot starve the user's other apps.
                options.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetSlidingWindowLimiter(
                    "mcp:" + PartitionKey(context),
                    _ => new SlidingWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6 }));
                options.AddPolicy(OAuthEndpoints.RegistrationRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                    "reg:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10) }));
                options.AddPolicy(OAuthEndpoints.TokenRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                    "tok:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
            });

            services.AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation { Name = "schuly", Title = "Schuly", Version = "1.0.0" };
                    options.ServerInstructions = "Schuly gives access to a student's school data (grades, exams, agenda, absences, documents) and their linked Microsoft 365 account (mail, Teams, OneDrive, OneNote). Which tools exist depends on what the user allowed for this connection. Tools that send, report or delete act for the user immediately: confirm with the user before calling them.";
                })
                .WithHttpTransport(options => options.Stateless = true)
                // Every tools/call is logged with its verdict before anything else runs. A denied call is answered here with an explanation the model can
                // pass on; the SDK's authorization filters below would refuse it anyway, and they are what hides the tool from tools/list.
                .WithMessageFilters(filters => filters.AddIncomingFilter(next => async (context, cancellationToken) =>
                {
                    if (context.JsonRpcMessage is JsonRpcRequest { Method: RequestMethods.ToolsCall } request
                        && request.Params?["name"]?.GetValue<string>() is { } name
                        && McpToolCatalog.Find(name) is { } metadata
                        && context.Services?.GetService<IHttpContextAccessor>()?.HttpContext is { } http
                        && http.Items[typeof(McpCaller)] is McpCaller caller)
                    {
                        var allowed = McpPolicies.Satisfies(http.User, metadata);
                        await http.RequestServices.GetRequiredService<McpActivityLog>().RecordAsync(caller, name, allowed, cancellationToken);
                        if (!allowed)
                        {
                            await context.Server.SendMessageAsync(new JsonRpcError { Id = request.Id, Error = new JsonRpcErrorDetail { Code = (int)McpErrorCode.InvalidRequest, Message = $"This connection is not allowed to use {name} ({McpPolicies.Describe(metadata)}). The user can change what this app may do on the MCP page in Schuly." } }, cancellationToken);
                            return;
                        }
                    }
                    await next(context, cancellationToken);
                }))
                .AddAuthorizationFilters()
                .WithTools<SchulyTools>()
                .WithTools<SchulnetzTools>()
                .WithTools<MicrosoftTools>();

            return services;
        }

        /// <summary>
        /// Maps <c>/mcp</c> behind the opaque-token policy, plus the protected resource metadata. Every method on <c>/mcp</c> (POST, GET, DELETE) needs a valid
        /// credential before the SDK sees the request, so nothing about the server, not even its tool names, is visible without one.
        /// </summary>
        /// <summary>Goes before authentication, because the SDK's handler answers the protected resource metadata from inside the authentication middleware.</summary>
        public static WebApplication UseSchulyMcpCors(this WebApplication app)
        {
            app.UseWhen(context => context.Request.Path.StartsWithSegments("/.well-known") || context.Request.Path.StartsWithSegments("/oauth") || context.Request.Path.StartsWithSegments(McpPublicUrls.ResourcePath), branch => branch.UseCors(CorsPolicy));
            return app;
        }

        /// <summary>Goes after authentication and before the rate limiter, so MCP requests are limited per connection and per user rather than per IP.</summary>
        public static WebApplication UseSchulyMcpGuards(this WebApplication app)
        {
            app.UseWhen(context => context.Request.Path.StartsWithSegments(McpPublicUrls.ResourcePath), branch => branch.UseMiddleware<McpRequestGuard>());

            // The consent page must never render inside someone else's frame, or a click on "Allow" could be stolen.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments(OAuthEndpoints.ConsentPath))
                {
                    context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
                    context.Response.Headers.XFrameOptions = "DENY";
                    context.Response.Headers.CacheControl = "no-store";
                }
                await next(context);
            });
            return app;
        }

        public static WebApplication MapSchulyMcp(this WebApplication app)
        {
            app.MapMcp(McpPublicUrls.ResourcePath)
                .RequireAuthorization(McpPolicies.Endpoint)
                .RequireRateLimiting(RateLimitPolicy)
                .WithHttpLogging(Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.None);

            // RFC 9728 places the metadata for /mcp at the path-inserted URL; the SDK's handler serves the root one named in WWW-Authenticate. Serve both.
            app.MapGet(ProtectedResourceMetadataPath + McpPublicUrls.ResourcePath, (McpPublicUrls urls) => Results.Json(ResourceMetadata(urls)))
                .AllowAnonymous()
                .ExcludeFromDescription();

            return app;
        }

        private static ProtectedResourceMetadata ResourceMetadata(McpPublicUrls urls) => new()
        {
            Resource = urls.Resource,
            AuthorizationServers = [urls.Issuer],
            ScopesSupported = McpScopes.Supported.ToList(),
            BearerMethodsSupported = ["header"],
            ResourceName = "Schuly",
            ResourceDocumentation = urls.Issuer + "/ai/mcp",
        };

        private static string PartitionKey(HttpContext context)
        {
            if (context.Items[typeof(McpCaller)] is McpCaller caller)
                return caller.GrantId?.ToString() ?? caller.ApiKeyId?.ToString() ?? "user:" + caller.ApplicationUserId;
            return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }
    }
}
