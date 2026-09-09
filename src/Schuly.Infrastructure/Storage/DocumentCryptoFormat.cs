using System.Buffers.Binary;

namespace Schuly.Infrastructure.Storage
{
    /// <summary>
    /// The on-disk layout for encrypted documents (format "SCHDOC1") and the small
    /// pieces of crypto plumbing (nonce derivation, chunk AAD) shared by the encrypting
    /// and decrypting streams. Every integer is little-endian; see the header layout
    /// table in the design notes for byte offsets.
    /// </summary>
    internal static class DocumentCryptoFormat
    {
        public static readonly byte[] Magic = "SCHDOC1"u8.ToArray();

        public const byte FormatVersion = 1;

        public const int MagicLength = 7;
        public const int FormatVersionOffset = 7;
        public const int KeyVersionOffset = 8;
        public const int WrappedDekLengthOffset = 12;
        public const int WrappedDekOffset = 14;

        public const int NonceSize = 12;
        public const int TagSize = 16;
        public const int DekSize = 32;
        public const int WrappedDekLength = NonceSize + DekSize + TagSize; // 60
        public const int AadPrefixLength = MagicLength + 1 + 4; // magic + format version + key version = 12
        public const int FrameLengthPrefixSize = 4;
        public const int FrameOverhead = FrameLengthPrefixSize + TagSize; // 20

        public const int MinChunkSizeBytes = 64;
        public const int MaxChunkSizeBytes = 16 * 1024 * 1024;
        public const int DefaultChunkSizeBytes = 1024 * 1024;

        public static int HeaderLength(int wrappedDekLength) => WrappedDekOffset + wrappedDekLength + NonceSize + 4;

        public static byte[] BuildHeader(int keyVersion, byte[] wrappedDek, byte[] baseNonce, int chunkSize)
        {
            var header = new byte[HeaderLength(wrappedDek.Length)];
            Magic.CopyTo(header, 0);
            header[FormatVersionOffset] = FormatVersion;
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(KeyVersionOffset, 4), keyVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(WrappedDekLengthOffset, 2), (ushort)wrappedDek.Length);
            wrappedDek.CopyTo(header, WrappedDekOffset);
            var baseNonceOffset = WrappedDekOffset + wrappedDek.Length;
            baseNonce.CopyTo(header, baseNonceOffset);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(baseNonceOffset + NonceSize, 4), chunkSize);
            return header;
        }

        public static byte[] BuildAadPrefix(int keyVersion)
        {
            var prefix = new byte[AadPrefixLength];
            Magic.CopyTo(prefix, 0);
            prefix[MagicLength] = FormatVersion;
            BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(MagicLength + 1, 4), keyVersion);
            return prefix;
        }

        /// <summary>
        /// Nonce for chunk <paramref name="chunkIndex"/>: the base nonce with its last
        /// 8 bytes XORed with the big-endian chunk index. Deterministic and distinct
        /// per chunk without needing to persist per-chunk nonces.
        /// </summary>
        public static byte[] ComputeChunkNonce(ReadOnlySpan<byte> baseNonce, long chunkIndex)
        {
            var nonce = baseNonce.ToArray();
            Span<byte> indexBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(indexBytes, chunkIndex);
            for (var i = 0; i < 8; i++)
                nonce[4 + i] ^= indexBytes[i];
            return nonce;
        }

        /// <summary>
        /// AAD for chunk <paramref name="chunkIndex"/>: the whole header, then the chunk
        /// index (int64 LE), then a final-chunk flag byte. Binding the header into every
        /// chunk's tag means tampering with the header (key version, base nonce, wrapped
        /// DEK) is caught the moment any chunk is decrypted, and binding the index plus
        /// the final flag catches chunk reordering, duplication and truncation.
        /// </summary>
        public static byte[] BuildChunkAad(ReadOnlySpan<byte> header, long chunkIndex, bool isFinal)
        {
            var aad = new byte[header.Length + 8 + 1];
            header.CopyTo(aad);
            BinaryPrimitives.WriteInt64LittleEndian(aad.AsSpan(header.Length, 8), chunkIndex);
            aad[^1] = isFinal ? (byte)1 : (byte)0;
            return aad;
        }
    }
}
