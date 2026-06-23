using System.Diagnostics.CodeAnalysis;

namespace Schuly.Infrastructure.Vault
{
    /// <summary>
    /// A small per-namespace secret store kept in process memory. Values are held
    /// <b>encrypted</b> (AES-256-GCM) with a key the host derives from a master
    /// secret it generates at startup, so inspecting the backing store — a heap or
    /// pointer dump — only ever reveals ciphertext. Plaintext exists transiently,
    /// just while a <see cref="Get"/> call decrypts. Each namespace (one per plugin,
    /// plus one for the host) is cryptographically isolated: its values can only be
    /// read back through the vault instance that holds its derived key.
    /// </summary>
    public interface IPluginVault
    {
        /// <summary>Encrypts and stores <paramref name="value"/> under <paramref name="key"/>, replacing any existing value.</summary>
        void Set(string key, string value);

        /// <summary>Decrypts and returns the value, or <c>null</c> if the key is absent.</summary>
        string? Get(string key);

        /// <summary>Decrypts the value if present. Returns <c>false</c> (and a null value) when the key is absent.</summary>
        bool TryGet(string key, [NotNullWhen(true)] out string? value);

        /// <summary>True if a value is stored under <paramref name="key"/>.</summary>
        bool Contains(string key);

        /// <summary>Removes the value under <paramref name="key"/>. Returns whether anything was removed.</summary>
        bool Remove(string key);

        /// <summary>Drops every value in this vault.</summary>
        void Clear();

        /// <summary>Number of stored entries.</summary>
        int Count { get; }
    }
}
