namespace TeilOne.FastZip
{
    public sealed class ZipEntry : ZipEntryHeader, IDisposable, IAsyncDisposable
    {
        private readonly Stream _stream;

        public ZipEntry(string fileName, CompressionMethod compressionMethod, bool isCompressedFileSizeNotInHeader, Stream stream)
            : base(fileName, compressionMethod, isCompressedFileSizeNotInHeader)
        {
            _stream = stream;
        }

        public ZipEntry(ZipEntryHeader header, Stream stream)
            : this(header.FullName, header.CompressionMethod, header.IsCompressedFileSizeNotInHeader, stream)
        {
            Crc32 = header.Crc32;
            CompressedLength = header.CompressedLength;
            Length = header.Length;
        }

        public Stream Stream => _stream;

        public void Dispose()
        {
            _stream.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _stream.DisposeAsync();
        }
    }
}