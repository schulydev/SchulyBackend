namespace Schuly.Infrastructure.Storage
{
    /// <summary>
    /// Replays a handful of already-consumed bytes before continuing to read from the
    /// wrapped stream. Used for legacy plaintext documents: detecting the encryption
    /// magic requires peeking the first bytes of the object, and those bytes must not
    /// be lost when the object turns out to be plaintext and gets streamed through as-is.
    /// </summary>
    internal sealed class PrefixedStream(byte[] prefix, Stream inner) : Stream
    {
        private int _prefixPos;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            if (_prefixPos < prefix.Length)
            {
                var n = Math.Min(buffer.Length, prefix.Length - _prefixPos);
                prefix.AsSpan(_prefixPos, n).CopyTo(buffer);
                _prefixPos += n;
                return n;
            }
            return inner.Read(buffer);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (buffer.Length == 0) return 0;
            if (_prefixPos < prefix.Length)
            {
                var n = Math.Min(buffer.Length, prefix.Length - _prefixPos);
                prefix.AsSpan(_prefixPos, n).CopyTo(buffer.Span);
                _prefixPos += n;
                return n;
            }
            return await inner.ReadAsync(buffer, ct);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync() => await inner.DisposeAsync();
    }
}
