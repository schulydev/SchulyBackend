using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Schuly.API.Extensions
{
    public static class DevAuthDefaults
    {
        public const string Section = "DevAuth";
        public const string DefaultIssuer = "schuly-dev";

        public const string DefaultSigningKey = "schuly-dev-fake-oidc-signing-key-change-me-0123456789";

        public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
            environment.IsDevelopment() && configuration.GetValue($"{Section}:Enabled", false);

        public static string Issuer(IConfiguration configuration) =>
            configuration[$"{Section}:Issuer"] ?? DefaultIssuer;

        public static SymmetricSecurityKey SigningKey(IConfiguration configuration) =>
            new(Encoding.UTF8.GetBytes(configuration[$"{Section}:SigningKey"] ?? DefaultSigningKey));
    }
}
