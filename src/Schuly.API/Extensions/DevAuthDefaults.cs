using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Schuly.API.Extensions
{
    /// <summary>
    /// Defaults for the development-only fake-OIDC token path. Enabled via the
    /// <c>DevAuth:Enabled</c> flag (set only in appsettings.Development.json).
    /// Never enable this outside Development — it trusts locally minted tokens.
    /// </summary>
    public static class DevAuthDefaults
    {
        public const string Section = "DevAuth";
        public const string DefaultIssuer = "schuly-dev";

        // 32+ chars so HMAC-SHA256 has a 256-bit key. Override via DevAuth:SigningKey.
        public const string DefaultSigningKey = "schuly-dev-fake-oidc-signing-key-change-me-0123456789";

        public static bool IsEnabled(IConfiguration configuration) =>
            configuration.GetValue($"{Section}:Enabled", false);

        public static string Issuer(IConfiguration configuration) =>
            configuration[$"{Section}:Issuer"] ?? DefaultIssuer;

        public static SymmetricSecurityKey SigningKey(IConfiguration configuration) =>
            new(Encoding.UTF8.GetBytes(configuration[$"{Section}:SigningKey"] ?? DefaultSigningKey));
    }
}
