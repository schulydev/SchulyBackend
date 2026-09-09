using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Schuly.Infrastructure.Storage
{
    /// <summary>
    /// Holds the master key (KEK) used to wrap per-document data-encryption keys, plus
    /// any previous key versions kept around so older documents stay readable after a
    /// key rotation. Registered as a singleton: the key material is parsed once at
    /// startup and reused for the app's lifetime rather than re-parsed per request.
    /// </summary>
    public sealed class DocumentEncryptionKeyring
    {
        private readonly byte[] _currentKey;
        private readonly Dictionary<int, byte[]> _previousKeys;

        public DocumentEncryptionKeyring(IOptions<DocumentEncryptionOptions> options, ILogger<DocumentEncryptionKeyring> logger)
        {
            var opts = options.Value;
            CurrentKeyVersion = opts.KeyVersion;
            ChunkSizeBytes = opts.ChunkSizeBytes;

            if (ChunkSizeBytes < DocumentCryptoFormat.MinChunkSizeBytes || ChunkSizeBytes > DocumentCryptoFormat.MaxChunkSizeBytes)
                throw new InvalidOperationException($"Documents:ChunkSizeBytes must be between {DocumentCryptoFormat.MinChunkSizeBytes} and {DocumentCryptoFormat.MaxChunkSizeBytes} bytes.");

            if (string.IsNullOrWhiteSpace(opts.EncryptionKey))
            {
                _currentKey = RandomNumberGenerator.GetBytes(DocumentCryptoFormat.DekSize);
                logger.LogWarning("Documents:EncryptionKey is not configured; using an ephemeral key. Documents encrypted now cannot be read after a restart.");
            }
            else
            {
                _currentKey = ParseKey(opts.EncryptionKey, "Documents:EncryptionKey");
            }

            _previousKeys = new Dictionary<int, byte[]>();
            foreach (var (version, base64) in opts.PreviousKeys)
                _previousKeys[version] = ParseKey(base64, $"Documents:PreviousKeys:{version}");
        }

        public int CurrentKeyVersion { get; }

        public int ChunkSizeBytes { get; }

        public ReadOnlySpan<byte> CurrentKey => _currentKey;

        public bool TryGetKey(int version, [NotNullWhen(true)] out byte[]? key)
        {
            if (version == CurrentKeyVersion)
            {
                key = _currentKey;
                return true;
            }

            return _previousKeys.TryGetValue(version, out key);
        }

        private static byte[] ParseKey(string base64, string settingName)
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException($"{settingName} is not valid base64.", ex);
            }

            if (key.Length != DocumentCryptoFormat.DekSize)
                throw new InvalidOperationException($"{settingName} must decode to exactly {DocumentCryptoFormat.DekSize} bytes, got {key.Length}.");

            return key;
        }

        /// <summary>
        /// Fails startup fast when running outside Development without a real master
        /// key configured - the ephemeral-key fallback in the constructor above would
        /// otherwise silently produce documents nobody can read after a restart.
        /// </summary>
        public static void ValidateConfiguration(IConfiguration configuration, bool isDevelopment)
        {
            if (isDevelopment)
                return;

            var key = configuration[$"{DocumentEncryptionOptions.SectionName}:EncryptionKey"];
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Documents:EncryptionKey must be configured outside Development.");

            ParseKey(key, "Documents:EncryptionKey");
        }
    }
}
