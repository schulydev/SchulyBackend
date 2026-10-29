using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>The issuer and the one resource this authorization server issues tokens for. Both are absolute, and the resource is what every token's audience is checked against.</summary>
    public class McpPublicUrls(IHttpContextAccessor httpContextAccessor, IOptions<McpOAuthOptions> options)
    {
        public const string ResourcePath = "/mcp";

        public string Issuer
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(options.Value.PublicUrl))
                    return options.Value.PublicUrl.TrimEnd('/');

                var request = httpContextAccessor.HttpContext?.Request ?? throw new InvalidOperationException("Mcp:PublicUrl is not set and there is no request to take the origin from.");
                return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
            }
        }

        public string Resource => Issuer + ResourcePath;
    }
}
