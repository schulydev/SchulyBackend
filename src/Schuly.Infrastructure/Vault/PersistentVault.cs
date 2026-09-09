using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Schuly.Infrastructure.Vault
{
    /// <summary>
    /// <see cref="IPluginVault"/> backed by Postgres (via <see cref="IVaultStore"/>) with
    /// an in-process <see cref="ConcurrentDictionary{TKey,TValue}"/> read-through cache
    /// whose values are AES-256-GCM ciphertext. Each entry is cached as
    /// <c>nonce(12) ‖ tag(16) ‖ ciphertext</c>, so the cache never holds a plaintext
    /// value. A fresh random nonce per write means writing the same value twice yields
    /// different ciphertext. GCM's auth tag also makes tampering detectable.
    /// </summary>
    internal sealed class PersistentVault : IPluginVault, IDisposable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int CurrentKeyVersion = 1;

        private readonly string _namespace;
        private readonly byte[] _key;
        private readonly IVaultStore _backing;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        private readonly object _hydrateLock = new();
        private volatile bool _hydrated;

        public PersistentVault(string @namespace, byte[] key, IVaultStore store, ILogger? logger = null)
        {
            if (key is not { Length: 32 })
                throw new ArgumentException("Vault key must be 32 bytes.", nameof(key));
            _namespace = @namespace;
            _key = key;
            _backing = store;
            _logger = logger;
        }

        public void Set(string key, string value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            EnsureHydrated();

            var plaintext = Encoding.UTF8.GetBytes(value);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(_key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            CryptographicOperations.ZeroMemory(plaintext);

            // DB first, cache second, so a failed write does not leave a phantom cached value.
            _backing.Save(_namespace, key, new VaultRecord(nonce, tag, ciphertext, CurrentKeyVersion));

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
            EnsureHydrated();
            if (key is null || !_store.TryGetValue(key, out var blob))
                return false;

            var nonce = blob.AsSpan(0, NonceSize);
            var tag = blob.AsSpan(NonceSize, TagSize);
            var ciphertext = blob.AsSpan(NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            try
            {
                using (var aes = new AesGcm(_key, TagSize))
                    aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                // Tampered or foreign-key entry — evict from the in-memory cache only,
                // leave the DB row alone so a key rollback can still recover it.
                _logger?.LogWarning("Vault entry {Namespace}/{Key} could not be decrypted with the current master key", _namespace, key);
                _store.TryRemove(key, out _);
                return false;
            }

            value = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return true;
        }

        public bool Contains(string key)
        {
            EnsureHydrated();
            return key is not null && _store.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            EnsureHydrated();
            if (key is null)
                return false;

            var removedFromBacking = _backing.Delete(_namespace, key);
            var removedFromCache = _store.TryRemove(key, out _);
            return removedFromBacking || removedFromCache;
        }

        public void Clear()
        {
            EnsureHydrated();
            _backing.DeleteAll(_namespace);
            _store.Clear();
        }

        public int Count
        {
            get
            {
                EnsureHydrated();
                return _store.Count;
            }
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(_key);

        private void EnsureHydrated()
        {
            if (_hydrated)
                return;

            lock (_hydrateLock)
            {
                if (_hydrated)
                    return;

                foreach (var (key, record) in _backing.Load(_namespace))
                {
                    var blob = new byte[NonceSize + TagSize + record.Ciphertext.Length];
                    record.Nonce.CopyTo(blob, 0);
                    record.Tag.CopyTo(blob, NonceSize);
                    record.Ciphertext.CopyTo(blob, NonceSize + TagSize);
                    _store[key] = blob;
                }

                _hydrated = true;
            }
        }
    }
}
