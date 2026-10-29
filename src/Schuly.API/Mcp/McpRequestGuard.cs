using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    /// <summary>Counts failed MCP credentials per IP, so guessing tokens costs a lockout long before it could succeed.</summary>
    public sealed class McpAuthFailureTracker(TimeProvider time)
    {
        public const int MaxFailures = 20;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

        private sealed class Entry
        {
            public int Count;
            public DateTimeOffset WindowStart;
            public DateTimeOffset? LockedUntil;
        }

        private readonly ConcurrentDictionary<string, Entry> _entries = new();

        public TimeSpan? LockedFor(string ip)
        {
            if (!_entries.TryGetValue(ip, out var entry))
                return null;
            lock (entry)
                return entry.LockedUntil is { } until && until > time.GetUtcNow() ? until - time.GetUtcNow() : null;
        }

        public void RecordFailure(string ip)
        {
            var now = time.GetUtcNow();
            var entry = _entries.GetOrAdd(ip, _ => new Entry { WindowStart = now });
            lock (entry)
            {
                if (now - entry.WindowStart > Window)
                {
                    entry.WindowStart = now;
                    entry.Count = 0;
                }
                if (++entry.Count >= MaxFailures)
                    entry.LockedUntil = now + Lockout;
            }

            if (_entries.Count > 50_000)
                foreach (var stale in _entries.Where(e => now - e.Value.WindowStart > Lockout + Window).Select(e => e.Key).ToList())
                    _entries.TryRemove(stale, out _);
        }
    }

    /// <summary>
    /// Runs in front of everything on <c>/mcp</c>: refuses a locked-out IP, and refuses browser origins other than this server's own (the DNS-rebinding guidance of
    /// the Streamable HTTP transport). Non-browser clients send no Origin and pass.
    /// </summary>
    public sealed class McpRequestGuard(RequestDelegate next, McpAuthFailureTracker failures, IOptions<McpOAuthOptions> options)
    {
        public async Task InvokeAsync(HttpContext context, McpPublicUrls urls)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (failures.LockedFor(ip) is { } wait)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();
                return;
            }

            string? origin = context.Request.Headers.Origin;
            if (!string.IsNullOrEmpty(origin) && !IsAllowedOrigin(origin, urls.Issuer, options.Value.AllowedOrigins))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Authenticated here, ahead of the rate limiter, so it can partition by connection and user. The handler caches the result for the endpoint's policy.
            var result = await context.AuthenticateAsync(McpTokenAuthenticationHandler.SchemeName);
            context.User = result.Succeeded ? result.Principal! : new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

            await next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized && !string.IsNullOrEmpty(context.Request.Headers.Authorization))
                failures.RecordFailure(ip);
        }

        public static bool IsAllowedOrigin(string origin, string issuer, IEnumerable<string>? extra) =>
            string.Equals(origin.TrimEnd('/'), issuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || (extra ?? []).Any(o => string.Equals(origin.TrimEnd('/'), o.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }
}
