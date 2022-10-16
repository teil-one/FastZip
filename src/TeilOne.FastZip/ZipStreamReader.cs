using CommunityToolkit.HighPerformance;
using System.IO.Compression;
using System.Text;

namespace TeilOne.FastZip
{
    public class ZipStreamReader : BaseStreamReader
    {
        public ZipStreamReader(Stream stream, int bufferSize = 1024 * 32)
            : base(stream, LocalFileHeaderSignature.Length, bufferSize)
        {
        }

        private static byte[] LocalFileHeaderSignature => new byte[] { 0x50, 0x4b, 0x03, 0x04 };

        private static byte[] CentralDirectoryFileHeaderSignature => new byte[] { 0x50, 0x4b, 0x01, 0x02 };

        private static ReadOnlySpan<byte> DataDescriptorSignature => new byte[] { 0x50, 0x4b, 0x07, 0x08 };


        public async IAsyncEnumerable<ZipEntry> GetEntriesAsync()
        {
            var entry = await ReadNextEntryAsync();

            while (entry != null)
            {
                yield return entry;

                entry = await ReadNextEntryAsync();
            }
        }

        private async Task<ZipEntry?> ReadNextEntryAsync()
        {
            var headerFound = await GotoNext(LocalFileHeaderSignature);

            if (headerFound)
            {
                return await ReadEntryAsync();
            }

            return null;
        }

        private async Task<ZipEntry> ReadEntryAsync()
        {
            var header = await ReadEntryHeaderAsync();
            var sourceStream = await ReadEntryDataAsync(header);

            Stream entryStream;
            switch (header.CompressionMethod)
            {
                case CompressionMethod.Store:
                    entryStream = sourceStream;
                    break;
                case CompressionMethod.Deflate:
                    entryStream = new DeflateStream(sourceStream, CompressionMode.Decompress);
                    break;
                default:
                    throw new NotSupportedException($"Compression method {header.CompressionMethod} is not supported");
            }


            var entry = new ZipEntry(header, entryStream);

            return entry;
        }

        private async Task<ZipEntryHeader> ReadEntryHeaderAsync()
        {
            var localFileHeader = await ReadNextAsync(30);

            var generalPurposeBitFlag = BitConverter.ToUInt16(localFileHeader.AsSpan(6, 2));
            var isCompressedFileSizeNotInHeader = (generalPurposeBitFlag & 0b1000) == 0x08;

            var compressionMethod = (CompressionMethod)BitConverter.ToUInt16(localFileHeader.AsSpan(8, 2));

            var crc32 = BitConverter.ToUInt32(localFileHeader.AsSpan(14, 4));
            var entryCompressedSize = BitConverter.ToUInt32(localFileHeader.AsSpan(18, 4));
            var entryUncompressedSize = BitConverter.ToUInt32(localFileHeader.AsSpan(22, 4));

            var fileNameLength = BitConverter.ToUInt16(localFileHeader.AsSpan(26, 2));

            var fileNameData = await ReadNextAsync(fileNameLength);
            var fileName = Encoding.UTF8.GetString(fileNameData);

            var extraFieldLength = BitConverter.ToUInt16(localFileHeader.AsSpan(28, 2));
            if (extraFieldLength > 0)
            {
                await ReadNextAsync(extraFieldLength); // Actual file data starts after the extra field
            }

            return new ZipEntryHeader(fileName, compressionMethod, isCompressedFileSizeNotInHeader)
            {
                Crc32 = crc32,
                Length = entryUncompressedSize,
                CompressedLength = entryCompressedSize
            };
        }

        private async Task<Stream> ReadEntryDataAsync(ZipEntryHeader entry)
        {
            var entryDataStream = await ReadUntil(LocalFileHeaderSignature, CentralDirectoryFileHeaderSignature);

            if (entryDataStream == null)
            {
                throw new InvalidDataException("Invalid ZIP file format");
            }

            if (entry.IsCompressedFileSizeNotInHeader)
            {
                await ParseAndTrimEntryDataDescriptorAsync(entry, entryDataStream);
            }

            if (entryDataStream.Length != entry.CompressedLength)
            {
                throw new InvalidDataException("Invalid data length");
            }

            return entryDataStream;
        }

        private static async Task ParseAndTrimEntryDataDescriptorAsync(ZipEntryHeader entry, Stream entryDataStream)
        {
            if (entryDataStream.Length < 20)
            {
                if (entryDataStream.Length < 12)
                {
                    throw new InvalidDataException("Unexpected end of data entry");
                }

                var dataDescriptor = await ReadDataDescriptorAsync(entryDataStream, 12);

                entry.Crc32 = BitConverter.ToUInt32(dataDescriptor.Slice(0, 4).Span);
                var compressedLength = BitConverter.ToInt32(dataDescriptor.Slice(4, 4).Span);
                entry.CompressedLength = compressedLength;
                entry.Length = BitConverter.ToInt32(dataDescriptor.Slice(8, 4).Span);

                entryDataStream.SetLength(compressedLength);
            }
            else
            {

                var dataDescriptor = await ReadDataDescriptorAsync(entryDataStream, 20);

                var zip64Crc32 = BitConverter.ToUInt32(dataDescriptor.Slice(0, 4).Span);
                var zip64CompressedLength = BitConverter.ToInt64(dataDescriptor.Slice(4, 8).Span);
                var zip64Length = BitConverter.ToInt64(dataDescriptor.Slice(12, 8).Span);

                var zip32Crc32 = BitConverter.ToUInt32(dataDescriptor.Slice(8, 4).Span);
                var zip32CompressedLength = BitConverter.ToInt32(dataDescriptor.Slice(12, 4).Span);
                var zip32Length = BitConverter.ToInt32(dataDescriptor.Slice(16, 4).Span);

                var isZip64 = !(zip32CompressedLength > 0
                    && zip32CompressedLength < entryDataStream.Length
                    && (entryDataStream.Length - zip32CompressedLength == 12 || entryDataStream.Length - zip32CompressedLength == 12 + DataDescriptorSignature.Length));

                if (isZip64)
                {
                    entryDataStream.SetLength(zip64CompressedLength);
                    entry.Crc32 = zip64Crc32;
                    entry.CompressedLength = zip64CompressedLength;
                    entry.Length = zip64Length;
                }
                else
                {
                    entryDataStream.SetLength(zip32CompressedLength);
                    entry.Crc32 = zip32Crc32;
                    entry.CompressedLength = zip32CompressedLength;
                    entry.Length = zip32Length;
                }
            }

            entryDataStream.Seek(0, SeekOrigin.Begin);
        }

        private static async Task<Memory<byte>> ReadDataDescriptorAsync(Stream entryDataStream, int descriptorLength)
        {
            entryDataStream.Seek(-descriptorLength, SeekOrigin.End);
            var dataDescriptor = new byte[descriptorLength].AsMemory();
            var byteRead = await entryDataStream.ReadAsync(dataDescriptor);
            if (byteRead != descriptorLength)
            {
                throw new InvalidDataException("Unexpected end of data descriptor");
            }

            return dataDescriptor;
        }
    }
}
