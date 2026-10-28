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

        string? ToPublicUserUrl(Guid applicationUserId, string? stored);

        bool VerifyUser(Guid applicationUserId, long exp, string sig);
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

        public string? ToPublicUrl(Guid schoolUserId, string? stored) => Mint(schoolUserId, stored, "", "/api/avatars");

        public string? ToPublicUserUrl(Guid applicationUserId, string? stored) => Mint(applicationUserId, stored, "user:", "/api/avatars/users");

        public bool Verify(Guid schoolUserId, long exp, string sig) => Check(schoolUserId, exp, sig, "");

        public bool VerifyUser(Guid applicationUserId, long exp, string sig) => Check(applicationUserId, exp, sig, "user:");

        private string? Mint(Guid id, string? stored, string purpose, string route)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return stored; // external URL (e.g. OIDC) — pass through

            // Align expiry to the next window boundary so the URL is stable for
            // up to WindowSeconds and cacheable, then rotates.
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = ((now / WindowSeconds) + 2) * WindowSeconds;
            var sig = Compute(id, exp, purpose);
            return $"{route}/{id}?exp={exp}&sig={sig}";
        }

        private bool Check(Guid id, long exp, string sig, string purpose)
        {
            if (exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
            var expected = Compute(id, exp, purpose);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig));
        }

        private string Compute(Guid id, long exp, string purpose)
        {
            using var hmac = new HMACSHA256(Key());
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{purpose}{id:N}:{exp}"));
            return Convert.ToHexStringLower(hash);
        }
    }
}
