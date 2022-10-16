namespace TeilOne.FastZip.Tests
{
    internal sealed class ThrottledStream : Stream
    {
        private readonly Stream _baseStream;
        private int _readSizeLimit;
        private bool _disposedValue;

        public ThrottledStream(Stream baseStream, int readSizeLimit)
        {
            _baseStream = baseStream;
            _readSizeLimit = readSizeLimit;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_readSizeLimit > 0 && _readSizeLimit < count)
            {
                return _baseStream.Read(buffer, offset, _readSizeLimit);
            }

            return _baseStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_readSizeLimit > 0 && _readSizeLimit < count)
            {
                return _baseStream.ReadAsync(buffer, offset, _readSizeLimit, cancellationToken);
            }
            return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _baseStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _baseStream.Dispose();
                }

                _disposedValue = true;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _baseStream.DisposeAsync();

            await base.DisposeAsync();
        }
    }
}
