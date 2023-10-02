namespace TeilOne.FastZip
{
    public class BaseStreamReader : IDisposable, IAsyncDisposable
    {
        private const int MaxEntryDataLengthInRam = 1024 * 512; // 512 KB

        private readonly byte[] _buffer;
        private readonly int _maxSearchLength;
        private readonly Stream _stream;
        private int _bufferPosition;
        private bool _disposed;
        private int _validBufferLength;

        public BaseStreamReader(Stream stream, int searchablePatternLength, int bufferSize)
        {
            if (searchablePatternLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(searchablePatternLength), "Searchable pattern length cannot be negative");
            }

            if (bufferSize < searchablePatternLength)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size is too small for the specified searchable pattern length");
            }

            _stream = stream;
            _maxSearchLength = searchablePatternLength;
            _buffer = new byte[bufferSize];

            _validBufferLength = 0;
            _bufferPosition = 0;
        }

        public BaseStreamReader(Stream stream, int searchablePatternLength, byte[] buffer)
        {
            if (searchablePatternLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(searchablePatternLength), "Searchable pattern length cannot be negative");
            }

            if (buffer.Length < searchablePatternLength)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer size is too small for the specified searchable pattern length");
            }

            _stream = stream;
            _maxSearchLength = searchablePatternLength;
            _buffer = buffer;

            _validBufferLength = 0;
            _bufferPosition = 0;
        }

        public void CompactBuffer()
        {
            Buffer.BlockCopy(_buffer, _bufferPosition, _buffer, 0, _validBufferLength - _bufferPosition);

            _validBufferLength -= _bufferPosition;
            _bufferPosition -= _bufferPosition;
        }

        public async Task<int> ReadMoreAsync()
        {
            if (_validBufferLength >= _buffer.Length)
            {
                throw new InvalidOperationException("Can't read - the buffer is full");
            }

            var bytesRead = await _stream.ReadAsync(_buffer, _validBufferLength, _buffer.Length - _validBufferLength);

            _validBufferLength += bytesRead;

            return bytesRead;
        }

        public async Task<byte[]> ReadNextAsync(int count)
        {
            if (count > _buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Can't read more than buffer size");
            }

            // No need to read from the stream
            if (_validBufferLength - _bufferPosition >= count)
            {
                _bufferPosition += count;
                return _buffer.AsSpan(_bufferPosition - count, count).ToArray();
            }

            // Need to shift before reading from the stream
            if (_bufferPosition + count > _buffer.Length)
            {
                CompactBuffer();
            }

            int bytesRead;
            do
            {
                bytesRead = await ReadMoreAsync();
            }
            while (bytesRead > 0 && _validBufferLength - _bufferPosition < count);

            if (_validBufferLength - _bufferPosition < count)
            {
                throw new InvalidOperationException("Can't read over the end of the stream");
            }

            _bufferPosition += count;
            return _buffer.AsSpan(_bufferPosition - count, count).ToArray();
        }

        public async Task<bool> GotoNext(byte[] search)
        {
            if (search.Length > _maxSearchLength)
            {
                throw new ArgumentException($"The search length {search.Length} is bigger than supported ({_maxSearchLength})", nameof(search));
            }

            int bytesRead;
            do
            {
                bytesRead = 0;

                var headerFound = _buffer.AsSpan(_bufferPosition, _validBufferLength - _bufferPosition).IndexOf(search);

                if (headerFound >= 0)
                {
                    _bufferPosition += headerFound;

                    return true;
                }

                ReadBuffer();

                if (_bufferPosition > 0)
                {
                    CompactBuffer();
                }

                if (_validBufferLength < _buffer.Length)
                {
                    bytesRead = await ReadMoreAsync();
                }
            }
            while (bytesRead > 0);

            return false;
        }

        public async Task<Stream?> ReadUntil(params byte[][] searchItems)
        {
            if (searchItems.Any(search => search.Length > _maxSearchLength))
            {
                throw new ArgumentException($"The search length is bigger than supported ({_maxSearchLength})", nameof(searchItems));
            }

            Stream result = new MemoryStream();

            int bytesRead;
            do
            {
                bytesRead = 0;

                var end = FindInBuffer(searchItems);

                var endFound = end >= 0;

                if (endFound)
                {
                    var endData = await ReadNextAsync(end);
                    await result.WriteAsync(endData);

                    result.Seek(0, SeekOrigin.Begin);
                    return result;
                }

                var fileData = ReadBuffer();
                await result.WriteAsync(fileData);

                if (result is MemoryStream && result.Length > MaxEntryDataLengthInRam)
                {
                    result = await MoveStreamToFile(result);
                }

                if (_bufferPosition > 0)
                {
                    CompactBuffer();
                }

                if (_validBufferLength < _buffer.Length)
                {
                    bytesRead = await ReadMoreAsync();
                }
            }
            while (bytesRead > 0);

            return null;
        }

        private int FindInBuffer(byte[][] searchItems)
        {
            var found = int.MaxValue;

            var span = _buffer.AsSpan(_bufferPosition, _validBufferLength - _bufferPosition);

            foreach (var search in searchItems)
            {
                var searchResult = span.IndexOf(search);
                if (searchResult >= 0 && searchResult < found)
                {
                    found = searchResult;
                }
            }

            return found == int.MaxValue ? -1 : found;
        }

        private async Task<FileStream> MoveStreamToFile(Stream sourceStream)
        {
            var file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var fileStream = new FileStream(
                file,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1024 * 1024,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            sourceStream.Seek(0, SeekOrigin.Begin);
            await sourceStream.CopyToAsync(fileStream);

            await sourceStream.DisposeAsync();

            return fileStream;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup
            await DisposeAsyncCore();

            // Dispose unmanaged resources
            Dispose(false);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources
                _stream.Dispose();
            }

            // Dispose unmanaged resources

            _disposed = true;
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            await _stream.DisposeAsync();
        }

        private Memory<byte> ReadBuffer()
        {
            if (_validBufferLength - _maxSearchLength + 1 - _bufferPosition <= 0)
            {
                return Array.Empty<byte>();
            }

            // Don't read the very end of the buffer to avoid splitting the searchable patthern
            var result = _buffer.AsMemory(_bufferPosition, _validBufferLength - _maxSearchLength + 1 - _bufferPosition);

            _bufferPosition = _validBufferLength - _maxSearchLength + 1;

            return result;
        }
    }
}
