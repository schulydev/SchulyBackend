using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Schuly.Infrastructure.Storage
{
    /// <summary>
    /// Read-only stream that serves an encrypted document's header followed by one
    /// AES-256-GCM frame per plaintext chunk, pulling from the source stream one chunk
    /// at a time so an upload never buffers the whole file. Supports rewinding to
    /// position 0 (but nothing else) because the AWS SDK rewinds request streams for
    /// checksums and retries; since the DEK and base nonce are fixed for the instance,
    /// a rewind reproduces byte-identical ciphertext.
    /// </summary>
    internal sealed class EncryptingReadStream : Stream
    {
        private readonly Stream _source;
        private readonly long _sourceStartPosition;
        private readonly AesGcm _dekAes;
        private readonly byte[] _header;
        private readonly byte[] _baseNonce;
        private readonly int _chunkSize;

        private readonly byte[] _plainBuffer;
        private byte[] _outputBuffer;
        private int _outputPos;
        private int _outputLen;
        private bool _headerServed;
        private bool _completed;
        private long _chunkIndex;
        private long _totalEmitted;
        private long _plaintextBytesRead;

        private byte? _pendingByte;
        private bool _sourceEnded;

        public EncryptingReadStream(Stream source, byte[] dek, byte[] header, byte[] baseNonce, int chunkSize)
        {
            _source = source;
            _sourceStartPosition = source.CanSeek ? source.Position : 0;
            _dekAes = new AesGcm(dek, DocumentCryptoFormat.TagSize);
            CryptographicOperations.ZeroMemory(dek);
            _header = header;
            _baseNonce = baseNonce;
            _chunkSize = chunkSize;
            _plainBuffer = new byte[chunkSize];
            _outputBuffer = [];
        }

        public long PlaintextBytesRead => _plaintextBytesRead;

        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => _source.CanSeek;

        public override long Length
        {
            get
            {
                if (!_source.CanSeek)
                    throw new NotSupportedException("Length requires a seekable source stream.");

                var plaintextLength = _source.Length - _sourceStartPosition;
                var chunkCount = plaintextLength <= 0 ? 1 : (plaintextLength + _chunkSize - 1) / _chunkSize;
                return _header.Length + chunkCount * (DocumentCryptoFormat.FrameLengthPrefixSize + DocumentCryptoFormat.TagSize) + plaintextLength;
            }
        }

        public override long Position
        {
            get => _totalEmitted;
            set
            {
                if (value != 0)
                    throw new NotSupportedException("Only rewinding to position 0 is supported.");
                Seek(0, SeekOrigin.Begin);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin != SeekOrigin.Begin || offset != 0)
                throw new NotSupportedException("Only rewinding to position 0 is supported.");
            if (!_source.CanSeek)
                throw new NotSupportedException("Rewinding requires a seekable source stream.");

            _source.Position = _sourceStartPosition;
            _chunkIndex = 0;
            _headerServed = false;
            _completed = false;
            _outputBuffer = [];
            _outputPos = 0;
            _outputLen = 0;
            _pendingByte = null;
            _sourceEnded = false;
            _totalEmitted = 0;
            _plaintextBytesRead = 0;
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            if (_outputPos >= _outputLen)
            {
                if (_completed) return 0;
                FillNextOutput();
            }
            return CopyFromOutput(buffer);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (buffer.Length == 0) return 0;
            if (_outputPos >= _outputLen)
            {
                if (_completed) return 0;
                await FillNextOutputAsync(ct);
            }
            return CopyFromOutput(buffer.Span);
        }

        private int CopyFromOutput(Span<byte> dest)
        {
            var n = Math.Min(dest.Length, _outputLen - _outputPos);
            _outputBuffer.AsSpan(_outputPos, n).CopyTo(dest);
            _outputPos += n;
            _totalEmitted += n;
            return n;
        }

        private void FillNextOutput()
        {
            if (!_headerServed)
            {
                ServeHeader();
                return;
            }

            var (length, isFinal) = ReadPlaintextChunk();
            EmitChunk(length, isFinal);
        }

        private async ValueTask FillNextOutputAsync(CancellationToken ct)
        {
            if (!_headerServed)
            {
                ServeHeader();
                return;
            }

            var (length, isFinal) = await ReadPlaintextChunkAsync(ct);
            EmitChunk(length, isFinal);
        }

        private void ServeHeader()
        {
            _outputBuffer = _header;
            _outputLen = _header.Length;
            _outputPos = 0;
            _headerServed = true;
        }

        private void EmitChunk(int length, bool isFinal)
        {
            var nonce = DocumentCryptoFormat.ComputeChunkNonce(_baseNonce, _chunkIndex);
            var aad = DocumentCryptoFormat.BuildChunkAad(_header, _chunkIndex, isFinal);

            var frame = new byte[DocumentCryptoFormat.FrameLengthPrefixSize + length + DocumentCryptoFormat.TagSize];
            BinaryPrimitives.WriteInt32LittleEndian(frame, length);
            var ciphertext = frame.AsSpan(DocumentCryptoFormat.FrameLengthPrefixSize, length);
            var tag = frame.AsSpan(DocumentCryptoFormat.FrameLengthPrefixSize + length, DocumentCryptoFormat.TagSize);
            _dekAes.Encrypt(nonce, _plainBuffer.AsSpan(0, length), ciphertext, tag, aad);

            _outputBuffer = frame;
            _outputLen = frame.Length;
            _outputPos = 0;
            _plaintextBytesRead += length;
            _chunkIndex++;
            if (isFinal) _completed = true;
        }

        private (int Length, bool IsFinal) ReadPlaintextChunk()
        {
            var filled = 0;
            if (_pendingByte.HasValue)
            {
                _plainBuffer[0] = _pendingByte.Value;
                _pendingByte = null;
                filled = 1;
            }

            while (filled < _plainBuffer.Length)
            {
                var n = _source.Read(_plainBuffer.AsSpan(filled));
                if (n == 0) { _sourceEnded = true; break; }
                filled += n;
            }

            if (filled < _plainBuffer.Length)
                return (filled, true);

            if (_sourceEnded)
                return (filled, true);

            Span<byte> peek = stackalloc byte[1];
            var p = _source.Read(peek);
            if (p == 0)
            {
                _sourceEnded = true;
                return (filled, true);
            }

            _pendingByte = peek[0];
            return (filled, false);
        }

        private async ValueTask<(int Length, bool IsFinal)> ReadPlaintextChunkAsync(CancellationToken ct)
        {
            var filled = 0;
            if (_pendingByte.HasValue)
            {
                _plainBuffer[0] = _pendingByte.Value;
                _pendingByte = null;
                filled = 1;
            }

            while (filled < _plainBuffer.Length)
            {
                var n = await _source.ReadAsync(_plainBuffer.AsMemory(filled), ct);
                if (n == 0) { _sourceEnded = true; break; }
                filled += n;
            }

            if (filled < _plainBuffer.Length)
                return (filled, true);

            if (_sourceEnded)
                return (filled, true);

            var peekBuffer = new byte[1];
            var p = await _source.ReadAsync(peekBuffer.AsMemory(0, 1), ct);
            if (p == 0)
            {
                _sourceEnded = true;
                return (filled, true);
            }

            _pendingByte = peekBuffer[0];
            return (filled, false);
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _dekAes.Dispose();
            base.Dispose(disposing);
        }
    }
}
