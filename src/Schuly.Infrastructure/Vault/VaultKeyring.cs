using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Vault
{
    /// <summary>
    /// Holds the process-ephemeral master secret the vault system is keyed on.
    /// Generated fresh on startup (32 random bytes) and never persisted or exposed —
    /// only the running backend knows it, so values written by a previous process,
    /// or copied out of memory without it, can't be decrypted. Per-namespace keys are
    /// derived with HKDF-SHA256 so each vault gets an independent key it can't use to
    /// read another namespace's data.
    /// </summary>
    public sealed class VaultKeyring : IDisposable
    {
        private readonly byte[] _master = RandomNumberGenerator.GetBytes(32);

        /// <summary>Derives a stable 32-byte key for <paramref name="namespace"/> from the master secret.</summary>
        public byte[] DeriveKey(string @namespace)
        {
            ArgumentException.ThrowIfNullOrEmpty(@namespace);
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm: _master,
                outputLength: 32,
                salt: null,
                info: Encoding.UTF8.GetBytes($"schuly-vault:{@namespace}"));
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(_master);
    }
}
