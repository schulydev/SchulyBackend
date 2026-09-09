using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Vault
{
    public sealed class VaultKeyring : IDisposable
    {
        public const int MasterKeySize = 32;

        private readonly byte[] _master;

        public VaultKeyring(byte[] master)
        {
            if (master is not { Length: MasterKeySize })
                throw new ArgumentException($"Vault master key must be {MasterKeySize} bytes.", nameof(master));
            _master = (byte[])master.Clone();
        }

        public static VaultKeyring Ephemeral() => new(RandomNumberGenerator.GetBytes(MasterKeySize));

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
