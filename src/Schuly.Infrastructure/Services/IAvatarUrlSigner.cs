using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Schuly.Infrastructure.Services
{
    /// <summary>
    /// Mints and validates short-lived HMAC-signed avatar URLs. The signing key
    /// comes from config (<c>Avatar:SigningKey</c>) and is never stored in the DB.
    /// The DB holds only a bare blob key; a capability URL is minted per-access.
    /// </summary>
    public interface IAvatarUrlSigner
    {
        string? ToPublicUrl(Guid schoolUserId, string? stored);

        bool Verify(Guid schoolUserId, long exp, string sig);
    }

    public class AvatarUrlSigner(IConfiguration configuration) : IAvatarUrlSigner
    {
        // TTL for minted URLs. Aligned to a window so the URL is stable within it
        // (lets clients/img caches reuse it) yet rotates regularly.
        private static readonly long WindowSeconds = 3600;

        private byte[] Key()
        {
            var key = configuration["Avatar:SigningKey"]
                ?? throw new InvalidOperationException("Avatar:SigningKey is not configured.");
            return Encoding.UTF8.GetBytes(key);
        }

        public string? ToPublicUrl(Guid schoolUserId, string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return stored; // external URL (e.g. OIDC) — pass through

            // Align expiry to the next window boundary so the URL is stable for
            // up to WindowSeconds and cacheable, then rotates.
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = ((now / WindowSeconds) + 2) * WindowSeconds;
            var sig = Compute(schoolUserId, exp);
            return $"/api/avatars/{schoolUserId}?exp={exp}&sig={sig}";
        }

        public bool Verify(Guid schoolUserId, long exp, string sig)
        {
            if (exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
            var expected = Compute(schoolUserId, exp);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig));
        }

        private string Compute(Guid schoolUserId, long exp)
        {
            using var hmac = new HMACSHA256(Key());
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{schoolUserId:N}:{exp}"));
            return Convert.ToHexStringLower(hash);
        }
    }
}
