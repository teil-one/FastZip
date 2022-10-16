namespace TeilOne.FastZip
{
    public class ZipEntryHeader
    {
        private readonly CompressionMethod _compressionMethod;
        private readonly string _fullName;
        private readonly bool _isCompressedFileSizeNotInHeader;

        public ZipEntryHeader(string fileName, CompressionMethod compressionMethod, bool isCompressedFileSizeNotInHeader)
        {
            _fullName = fileName;
            _compressionMethod = compressionMethod;
            _isCompressedFileSizeNotInHeader = isCompressedFileSizeNotInHeader;
        }

        public string FullName => _fullName;

        public CompressionMethod CompressionMethod => _compressionMethod;

        public uint Crc32 { get; internal set; }

        public long Length { get; internal set; }

        public long CompressedLength { get; internal set; }

        internal bool IsCompressedFileSizeNotInHeader => _isCompressedFileSizeNotInHeader;
    }
}