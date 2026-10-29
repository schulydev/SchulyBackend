namespace Schuly.Infrastructure.Mcp
{
    /// <summary>Which redirect URIs a client may register, and when a requested one matches. Matching is exact string comparison, except that a loopback redirect may use any port (RFC 8252 section 7.3), since native apps bind whatever port is free.</summary>
    public static class RedirectUris
    {
        public const int MaxLength = 2000;

        private static readonly HashSet<string> ForbiddenSchemes = new(StringComparer.OrdinalIgnoreCase)
        {
            "javascript", "data", "vbscript", "file", "about", "blob", "filesystem", "ws", "wss", "ftp",
        };

        public static bool IsAllowedForRegistration(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;
            if (!string.IsNullOrEmpty(uri.Fragment) || value.Contains('#'))
                return false;
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return false;
            if (uri.Scheme == Uri.UriSchemeHttps)
                return true;
            if (uri.Scheme == Uri.UriSchemeHttp)
                return IsLoopback(uri);
            // Private-use schemes (cursor://, vscode://) are how desktop apps receive the code (RFC 8252 section 7.1).
            return !ForbiddenSchemes.Contains(uri.Scheme);
        }

        public static bool Matches(IEnumerable<string> registered, string? requested)
        {
            if (string.IsNullOrEmpty(requested) || !Uri.TryCreate(requested, UriKind.Absolute, out var wanted))
                return false;

            foreach (var candidate in registered)
            {
                if (string.Equals(candidate, requested, StringComparison.Ordinal))
                    return true;

                if (wanted.Scheme == Uri.UriSchemeHttp && IsLoopback(wanted) && Uri.TryCreate(candidate, UriKind.Absolute, out var allowed) && allowed.Scheme == Uri.UriSchemeHttp && IsLoopback(allowed)
                    && string.Equals(allowed.Host, wanted.Host, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(allowed.PathAndQuery, wanted.PathAndQuery, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsLoopback(Uri uri) => uri.Host is "127.0.0.1" or "[::1]" or "localhost" || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }
}
