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
        void Set(string key, string value);

        string? Get(string key);

        bool TryGet(string key, [NotNullWhen(true)] out string? value);

        bool Contains(string key);

        bool Remove(string key);

        void Clear();

        int Count { get; }
    }
}
