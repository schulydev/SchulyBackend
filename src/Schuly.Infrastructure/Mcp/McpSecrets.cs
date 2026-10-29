using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>Opaque MCP credentials: 256 random bits behind a recognisable prefix, stored only as a SHA-256 hash. A fast hash is enough because the input has full entropy and cannot be guessed.</summary>
    public static class McpSecrets
    {
        public const string ApiKeyPrefix = "schuly_mcp_";
        public const string AccessTokenPrefix = "schuly_mcpat_";
        public const string RefreshTokenPrefix = "schuly_mcprt_";
        public const string CodePrefix = "schuly_mcpac_";

        public static string NewSecret(string prefix) => prefix + Base64Url(RandomNumberGenerator.GetBytes(32));

        public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

        /// <summary>Constant time, so the comparison after the index lookup leaks nothing about how much of a hash matched.</summary>
        public static bool HashEquals(string a, string b) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

        public static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
