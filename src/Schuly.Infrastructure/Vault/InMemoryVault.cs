using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Schuly.Infrastructure.Vault
{
    /// <summary>
    /// In-memory <see cref="IPluginVault"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// whose values are AES-256-GCM ciphertext. Each entry is stored as
    /// <c>nonce(12) ‖ tag(16) ‖ ciphertext</c>, so the backing store never holds a
    /// plaintext value. A fresh random nonce per write means writing the same value
    /// twice yields different ciphertext. GCM's auth tag also makes tampering detectable.
    /// </summary>
    internal sealed class InMemoryVault : IPluginVault, IDisposable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly byte[] _key;
        private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public InMemoryVault(byte[] key)
        {
            if (key is not { Length: 32 })
                throw new ArgumentException("Vault key must be 32 bytes.", nameof(key));
            _key = key;
        }

        public void Set(string key, string value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);

            var plaintext = Encoding.UTF8.GetBytes(value);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(_key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            CryptographicOperations.ZeroMemory(plaintext);

            var blob = new byte[NonceSize + TagSize + ciphertext.Length];
            nonce.CopyTo(blob, 0);
            tag.CopyTo(blob, NonceSize);
            ciphertext.CopyTo(blob, NonceSize + TagSize);

            _store[key] = blob;
        }

        public string? Get(string key) => TryGet(key, out var value) ? value : null;

        public bool TryGet(string key, [NotNullWhen(true)] out string? value)
        {
            value = null;
            if (key is null || !_store.TryGetValue(key, out var blob))
                return false;

            var nonce = blob.AsSpan(0, NonceSize);
            var tag = blob.AsSpan(NonceSize, TagSize);
            var ciphertext = blob.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(_key, TagSize))
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

            value = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return true;
        }

        public bool Contains(string key) => key is not null && _store.ContainsKey(key);

        public bool Remove(string key) => key is not null && _store.TryRemove(key, out _);

        public void Clear() => _store.Clear();

        public int Count => _store.Count;

        public void Dispose() => CryptographicOperations.ZeroMemory(_key);
    }
}
