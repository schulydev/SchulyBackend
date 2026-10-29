using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Mcp
{
    /// <summary>RFC 7636 with the S256 method only; "plain" would let anyone who sees the authorization request redeem the code.</summary>
    public static class Pkce
    {
        public const string Method = "S256";

        public static bool IsValidChallenge(string? challenge) => challenge is { Length: 43 } && challenge.All(IsBase64UrlChar);

        public static bool IsValidVerifier(string? verifier) => verifier is { Length: >= 43 and <= 128 } && verifier.All(IsUnreserved);

        public static bool Verify(string? verifier, string challenge)
        {
            if (!IsValidVerifier(verifier))
                return false;

            var computed = McpSecrets.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier!)));
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(challenge));
        }

        public static string ChallengeFor(string verifier) => McpSecrets.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        private static bool IsBase64UrlChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_';

        private static bool IsUnreserved(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
    }
}
