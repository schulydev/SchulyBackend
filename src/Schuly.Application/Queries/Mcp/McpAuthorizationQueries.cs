using Mediator;
using Microsoft.AspNetCore.WebUtilities;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Models;
using Schuly.Infrastructure.Mcp;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Queries.Mcp
{
    /// <summary>Validates an authorization request for the consent screen. A request that may be redirected carries the error URL to send the browser back to.</summary>
    [AllowAuthenticated]
    public record GetMcpAuthorizationRequestQuery(McpAuthorizationParameters Parameters) : IQuery<Result<McpAuthorizationRequestDto>>;

    public class GetMcpAuthorizationRequestQueryHandler(McpOAuthService oauth, McpPublicUrls urls) : IQueryHandler<GetMcpAuthorizationRequestQuery, Result<McpAuthorizationRequestDto>>
    {
        public async ValueTask<Result<McpAuthorizationRequestDto>> Handle(GetMcpAuthorizationRequestQuery query, CancellationToken cancellationToken)
        {
            var request = query.Parameters.ToRequest();
            var check = await oauth.CheckAuthorizationRequestAsync(request, urls.Resource, cancellationToken);
            var host = check.RedirectUri is null ? null : McpOAuthService.RedirectHosts([check.RedirectUri]).FirstOrDefault();
            var redirect = check.Status == AuthorizationRequestStatus.Redirect ? McpRedirects.Error(check.RedirectUri!, check.Error!, check.ErrorDescription, request.State, urls.Issuer) : null;
            return Result.Success(new McpAuthorizationRequestDto(check.Status == AuthorizationRequestStatus.Valid, check.Error, check.ErrorDescription, check.ClientName, check.ClientUri, host, check.Requested, redirect));
        }
    }

    [AllowAuthenticated]
    public record GetMcpConnectionsQuery() : IQuery<Result<List<McpConnectionDto>>>;

    public class GetMcpConnectionsQueryHandler(McpOAuthService oauth, IUserService userService) : IQueryHandler<GetMcpConnectionsQuery, Result<List<McpConnectionDto>>>
    {
        public async ValueTask<Result<List<McpConnectionDto>>> Handle(GetMcpConnectionsQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var grants = await oauth.ListGrantsAsync(userId, cancellationToken);
            return Result.Success(grants.Select(g => McpRedirects.ToDto(g)).ToList());
        }
    }

    [AllowAuthenticated]
    public record GetMcpApiKeysQuery() : IQuery<Result<List<McpApiKeyDto>>>;

    public class GetMcpApiKeysQueryHandler(McpApiKeyService keys, IUserService userService) : IQueryHandler<GetMcpApiKeysQuery, Result<List<McpApiKeyDto>>>
    {
        public async ValueTask<Result<List<McpApiKeyDto>>> Handle(GetMcpApiKeysQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var list = await keys.ListAsync(userId, cancellationToken);
            return Result.Success(list.Select(McpRedirects.ToDto).ToList());
        }
    }

    /// <summary>The last tool calls of one connected app or one API key: tool name, time, allowed or denied.</summary>
    [AllowAuthenticated]
    public record GetMcpActivityQuery(Guid? ConnectionId, Guid? ApiKeyId) : IQuery<Result<List<McpActivityDto>>>;

    public class GetMcpActivityQueryHandler(McpActivityLog log, IUserService userService) : IQueryHandler<GetMcpActivityQuery, Result<List<McpActivityDto>>>
    {
        public async ValueTask<Result<List<McpActivityDto>>> Handle(GetMcpActivityQuery query, CancellationToken cancellationToken)
        {
            var userId = await userService.GetCurrentUserIdAsync(cancellationToken);
            var entries = query.ConnectionId is { } grantId
                ? await log.ListForGrantAsync(userId, grantId, cancellationToken)
                : query.ApiKeyId is { } keyId ? await log.ListForApiKeyAsync(userId, keyId, cancellationToken) : [];
            return Result.Success(entries.Select(e => new McpActivityDto(e.ToolName, e.Allowed, e.At)).ToList());
        }
    }

    public static class McpRedirects
    {
        /// <summary>RFC 9207: <c>iss</c> on every authorization response, so a client talking to several servers can tell which one answered.</summary>
        public static string Code(string redirectUri, string code, string? state, string issuer) => Build(redirectUri, new() { ["code"] = code, ["state"] = state, ["iss"] = issuer });

        public static string Error(string redirectUri, string error, string? description, string? state, string issuer) => Build(redirectUri, new() { ["error"] = error, ["error_description"] = description, ["state"] = state, ["iss"] = issuer });

        public static AuthorizationRequest ToRequest(this McpAuthorizationParameters p) => new(p.ResponseType, p.ClientId, p.RedirectUri, p.Scope, p.State, p.CodeChallenge, p.CodeChallengeMethod, p.Resource);

        public static McpApiKeyDto ToDto(McpApiKeySummary k) => new(k.Id, k.Name, k.Prefix, k.Permissions, k.CreatedAt, k.ExpiresAt, k.LastUsedAt);

        public static McpConnectionDto ToDto(McpGrantSummary g) => new(g.Id, g.ClientName, g.ClientUri, g.RedirectHosts, g.Permissions, g.CreatedAt, g.LastUsedAt);

        private static string Build(string redirectUri, Dictionary<string, string?> values) =>
            QueryHelpers.AddQueryString(redirectUri, values.Where(v => v.Value is not null));
    }
}
