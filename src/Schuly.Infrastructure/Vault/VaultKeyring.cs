using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Vault
{
    public sealed class VaultKeyring : IDisposable
    {
        private readonly byte[] _master = RandomNumberGenerator.GetBytes(32);

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
