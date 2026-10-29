using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Schuly.Domain.Mcp;
using Schuly.Infrastructure.Mcp;

namespace Schuly.API.Mcp
{
    /// <summary>
    /// The area and level a tool needs. It is an <see cref="AuthorizeAttribute"/>, so the MCP SDK's authorization filters both hide the tool from <c>tools/list</c>
    /// and refuse a call to it when the connection lacks the scope. The policy reads the principal's scope claims, which come from the live permissions on every request.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class McpAccessAttribute : AuthorizeAttribute
    {
        public McpAccessAttribute(McpArea area, McpAccessLevel level)
        {
            Area = area;
            Level = level;
            Policy = McpPolicies.For(McpScopes.For(area, level));
        }

        public McpArea Area { get; }
        public McpAccessLevel Level { get; }
    }

    /// <summary>Deleting needs the separate "allow destructive actions" permission on top of write access to the area.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class McpDestructiveAttribute : AuthorizeAttribute
    {
        public McpDestructiveAttribute() => Policy = McpPolicies.For(McpScopes.Destructive);
    }

    public static class McpPolicies
    {
        public const string Endpoint = "SchulyMcp";

        public static string For(string scope) => "mcp-scope:" + scope;

        public static void AddScopePolicies(AuthorizationOptions options)
        {
            foreach (var scope in McpScopes.Supported)
                options.AddPolicy(For(scope), policy => policy.RequireAuthenticatedUser().RequireClaim(McpScopes.ClaimType, scope));
        }

        /// <summary>What a tool needs, in words for an error message: "needs Mail read and write, and permission to delete".</summary>
        public static string Describe(IEnumerable<object> metadata)
        {
            var parts = new List<string>();
            foreach (var item in metadata)
            {
                if (item is McpAccessAttribute access)
                    parts.Add($"needs {access.Area} {(access.Level == McpAccessLevel.ReadWrite ? "read and write" : "read")} access");
                else if (item is McpDestructiveAttribute)
                    parts.Add("needs permission to delete");
            }
            return parts.Count == 0 ? "not permitted" : string.Join(", ", parts);
        }

        /// <summary>Whether a principal satisfies every permission attribute in a tool's metadata. Used for the activity log, which has to know the verdict without running the tool.</summary>
        public static bool Satisfies(ClaimsPrincipal user, IEnumerable<object> metadata)
        {
            foreach (var item in metadata)
            {
                var scope = item switch
                {
                    McpAccessAttribute access => McpScopes.For(access.Area, access.Level),
                    McpDestructiveAttribute => McpScopes.Destructive,
                    _ => null,
                };
                if (scope is not null && !user.HasClaim(McpScopes.ClaimType, scope))
                    return false;
            }
            return true;
        }
    }
}
