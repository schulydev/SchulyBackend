using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Schuly.Infrastructure.Storage
{
    /// <summary>
    /// Read-only stream that decrypts one AES-256-GCM frame at a time from an encrypted
    /// document's body, owning (and disposing) the inner ciphertext stream. Whether a
    /// frame is the last one is never trusted from the frame itself - it is discovered
    /// by looking one length-prefix ahead in the stream, and that discovered flag feeds
    /// straight into the AAD used to authenticate the frame. So truncation, reordering
    /// or duplication of frames does not just look wrong, it makes the GCM tag fail to
    /// verify.
    /// </summary>
    internal sealed class DecryptingReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly AesGcm _dekAes;
        private readonly byte[] _header;
        private readonly byte[] _baseNonce;
        private readonly int _chunkSize;

        private byte[] _plainBuffer = [];
        private int _plainPos;
        private int _plainLen;
        private long _chunkIndex;
        private bool _finalConsumed;

        private byte[]? _pendingLengthPrefix;

        public DecryptingReadStream(Stream inner, byte[] dek, byte[] header, byte[] baseNonce, int chunkSize)
        {
            _inner = inner;
            _dekAes = new AesGcm(dek, DocumentCryptoFormat.TagSize);
            CryptographicOperations.ZeroMemory(dek);
            _header = header;
            _baseNonce = baseNonce;
            _chunkSize = chunkSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            if (_plainPos >= _plainLen)
            {
                if (_finalConsumed) return 0;
                FillNextChunk();
            }
            return CopyOut(buffer);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (buffer.Length == 0) return 0;
            if (_plainPos >= _plainLen)
            {
                if (_finalConsumed) return 0;
                await FillNextChunkAsync(ct);
            }
            return CopyOut(buffer.Span);
        }

        private int CopyOut(Span<byte> dest)
        {
            var n = Math.Min(dest.Length, _plainLen - _plainPos);
            _plainBuffer.AsSpan(_plainPos, n).CopyTo(dest);
            _plainPos += n;
            return n;
        }

        private void FillNextChunk()
        {
            Span<byte> lenBuf = stackalloc byte[DocumentCryptoFormat.FrameLengthPrefixSize];
            if (_pendingLengthPrefix is { } pending)
            {
                pending.CopyTo(lenBuf);
                _pendingLengthPrefix = null;
            }
            else
            {
                ReadExactly(_inner, lenBuf);
            }

            var ctLen = ValidateChunkLength(lenBuf);
            var frame = new byte[ctLen + DocumentCryptoFormat.TagSize];
            ReadExactly(_inner, frame);

            Span<byte> nextLenBuf = stackalloc byte[DocumentCryptoFormat.FrameLengthPrefixSize];
            var hasMore = TryReadLookaheadPrefix(_inner, nextLenBuf);
            if (hasMore)
                _pendingLengthPrefix = nextLenBuf.ToArray();

            Decrypt(ctLen, frame, isFinal: !hasMore);
        }

        private async ValueTask FillNextChunkAsync(CancellationToken ct)
        {
            var lenBuf = new byte[DocumentCryptoFormat.FrameLengthPrefixSize];
            if (_pendingLengthPrefix is { } pending)
            {
                pending.CopyTo(lenBuf, 0);
                _pendingLengthPrefix = null;
            }
            else
            {
                await ReadExactlyAsync(_inner, lenBuf, ct);
            }

            var ctLen = ValidateChunkLength(lenBuf);
            var frame = new byte[ctLen + DocumentCryptoFormat.TagSize];
            await ReadExactlyAsync(_inner, frame, ct);

            var nextLenBuf = new byte[DocumentCryptoFormat.FrameLengthPrefixSize];
            var isFinal = !await TryReadLookaheadPrefixAsync(_inner, nextLenBuf, ct);
            if (!isFinal)
                _pendingLengthPrefix = nextLenBuf;

            Decrypt(ctLen, frame, isFinal);
        }

        private int ValidateChunkLength(ReadOnlySpan<byte> lenBuf)
        {
            var ctLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            if (ctLen < 0 || ctLen > _chunkSize)
                throw new CryptographicException("Invalid chunk length in encrypted document stream.");
            return ctLen;
        }

        private void Decrypt(int ctLen, byte[] frame, bool isFinal)
        {
            var nonce = DocumentCryptoFormat.ComputeChunkNonce(_baseNonce, _chunkIndex);
            var aad = DocumentCryptoFormat.BuildChunkAad(_header, _chunkIndex, isFinal);
            var plaintext = new byte[ctLen];
            _dekAes.Decrypt(nonce, frame.AsSpan(0, ctLen), frame.AsSpan(ctLen, DocumentCryptoFormat.TagSize), plaintext, aad);

            _plainBuffer = plaintext;
            _plainLen = ctLen;
            _plainPos = 0;
            _chunkIndex++;
            if (isFinal) _finalConsumed = true;
        }

        private static void ReadExactly(Stream s, Span<byte> buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = s.Read(buffer[total..]);
                if (n == 0)
                    throw new CryptographicException("Truncated encrypted document stream.");
                total += n;
            }
        }

        private static async ValueTask ReadExactlyAsync(Stream s, Memory<byte> buffer, CancellationToken ct)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = await s.ReadAsync(buffer[total..], ct);
                if (n == 0)
                    throw new CryptographicException("Truncated encrypted document stream.");
                total += n;
            }
        }

        /// <summary>Reads a full length-prefix ahead, returns false on clean EOF, throws on a short/partial read.</summary>
        private static bool TryReadLookaheadPrefix(Stream s, Span<byte> buffer)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = s.Read(buffer[total..]);
                if (n == 0)
                {
                    if (total == 0) return false;
                    throw new CryptographicException("Truncated encrypted document stream.");
                }
                total += n;
            }
            return true;
        }

        private static async ValueTask<bool> TryReadLookaheadPrefixAsync(Stream s, byte[] buffer, CancellationToken ct)
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = await s.ReadAsync(buffer.AsMemory(total), ct);
                if (n == 0)
                {
                    if (total == 0) return false;
                    throw new CryptographicException("Truncated encrypted document stream.");
                }
                total += n;
            }
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dekAes.Dispose();
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            _dekAes.Dispose();
            await _inner.DisposeAsync();
        }
    }
}
