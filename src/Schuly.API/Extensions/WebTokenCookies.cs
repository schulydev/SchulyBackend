using System.Text;
using System.Text.Json.Nodes;

namespace Schuly.API.Extensions
{
    /// <summary>Keeps the web app's refresh and device tokens in HttpOnly cookies instead of JavaScript.</summary>
    public class WebTokenCookies(RequestDelegate next)
    {
        public const string ClientHeader = "X-Schuly-Client";
        private const string RefreshCookie = "schuly_rt";
        private const string DeviceCookie = "schuly_dt";

        private static readonly HashSet<string> TokenIssuingPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/auth/login", "/auth/register", "/auth/refresh", "/auth/2fa/verify"
        };

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;
            if (!HttpMethods.IsPost(request.Method) || request.Headers[ClientHeader] != "web" || !request.Path.StartsWithSegments("/auth"))
            {
                await next(context);
                return;
            }

            var path = request.Path.Value ?? string.Empty;

            if (path.Equals("/auth/refresh", StringComparison.OrdinalIgnoreCase) || path.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase))
                await InjectAsync(request, "refreshToken", request.Cookies[RefreshCookie]);
            else if (path.Equals("/auth/login", StringComparison.OrdinalIgnoreCase))
                await InjectAsync(request, "deviceToken", request.Cookies[DeviceCookie]);

            if (path.Equals("/auth/logout", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                context.Response.Cookies.Delete(RefreshCookie, CookieOptions(context, TimeSpan.Zero));
                return;
            }

            if (!TokenIssuingPaths.Contains(path))
            {
                await next(context);
                return;
            }

            var original = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await next(context);
                buffer.Position = 0;
                var payload = buffer.ToArray();

                if (context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created
                    && context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
                    && JsonNode.Parse(payload) is JsonObject body)
                {
                    if (Take(body, "refresh_token") is { } refreshToken)
                        context.Response.Cookies.Append(RefreshCookie, refreshToken, CookieOptions(context, TimeSpan.FromDays(14)));
                    if (Take(body, "device_token") is { } deviceToken)
                        context.Response.Cookies.Append(DeviceCookie, deviceToken, CookieOptions(context, TimeSpan.FromDays(30)));

                    payload = Encoding.UTF8.GetBytes(body.ToJsonString());
                    context.Response.ContentLength = payload.Length;
                }
                else if (context.Response.StatusCode == StatusCodes.Status401Unauthorized && path.Equals("/auth/refresh", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Cookies.Delete(RefreshCookie, CookieOptions(context, TimeSpan.Zero));
                }

                context.Response.Body = original;
                await context.Response.Body.WriteAsync(payload, context.RequestAborted);
            }
            finally
            {
                context.Response.Body = original;
            }
        }

        private static string? Take(JsonObject body, string property)
        {
            var value = body[property]?.GetValue<string>();
            body.Remove(property);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static async Task InjectAsync(HttpRequest request, string property, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            request.EnableBuffering();
            var body = request.ContentLength > 0 ? await JsonNode.ParseAsync(request.Body) as JsonObject : null;
            body ??= [];
            if (body[property] is null)
                body[property] = value;

            var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
            request.Body = new MemoryStream(bytes);
            request.ContentLength = bytes.Length;
            request.ContentType = "application/json";
        }

        private static CookieOptions CookieOptions(HttpContext context, TimeSpan lifetime) => new()
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/auth",
            MaxAge = lifetime
        };
    }
}
