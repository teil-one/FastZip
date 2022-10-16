using NeoSmart.StreamCompare;
using System.IO.Compression;
using System.Text;

namespace TeilOne.FastZip.Tests
{
    [TestClass]
    public class ZipStreamReaderTest
    {
        [TestMethod]
        public async Task ZipStreamReader_StoreWithOptionalDataDescriptor_ReturnsCorrectContent()
        {
            await TestZipContent("TestData/zip-store-data-descriptor.zip", keepFile: true);
        }

        [TestMethod]
        public async Task ZipStreamReader_DeflateWithOptionalDataDescriptor_ReturnsCorrectContent()
        {
            await TestZipContent("TestData/zip-deflate-data-descriptor.zip", keepFile: true);
        }

        [TestMethod]
        public async Task ZipStreamReader_DeflateOneByte_ReturnsCorrectContent()
        {
            await TestZipContent("TestData/zip-deflate-one-byte.zip", keepFile: true);
        }

        [TestMethod]
        public async Task ZipStreamReader_Deflate_ReturnsCorrectContent()
        {
            await TestZipContent(CreateTestZip("zip-deflate.zip", CompressionLevel.Fastest, 16384));
        }

        [TestMethod]
        [Timeout(1000)]
        public async Task ZipStreamReader_SlowSource_ReturnsCorrectContent()
        {
            var filePath = CreateTestZip("zip-deflate.zip", CompressionLevel.Fastest, 16384);

            try
            {
                using var zipStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var throttledZipStream = new ThrottledStream(zipStream, 5);
                using var expectedStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                await TestZipContent(expectedStream, throttledZipStream);
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task ZipStreamReader_NoCompression_ReturnsCorrectContent()
        {
            await TestZipContent(CreateTestZip("zip-store.zip", CompressionLevel.NoCompression, 16384));
        }

        [TestMethod]
        public async Task ZipStreamReader_BrokenFile_ThrowsFormatException()
        {
            var filePath = CreateTestZip("broken-file.zip", CompressionLevel.NoCompression, 16384);
            var data = File.ReadAllBytes(filePath);
            File.WriteAllBytes(filePath, data.AsSpan(0, data.Length / 2).ToArray());

            await Assert.ThrowsExceptionAsync<InvalidDataException>(async () =>
            {
                await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                await using var reader = new ZipStreamReader(fs);

                var numEntries = await reader.GetEntriesAsync().CountAsync();
            });

            File.Delete(filePath);
        }

        [TestMethod]
        public async Task ZipStreamReader_Zip64Deflate_ReturnsCorrectContent()
        {
            await TestZipContent(CreateTestZip("zip64-deflate.zip", CompressionLevel.Fastest, UInt32.MaxValue));
        }

        [TestMethod]
        public async Task ZipStreamReader_Zip64Store_ReturnsCorrectContent()
        {
            await TestZipContent(CreateTestZip("zip64-store.zip", CompressionLevel.NoCompression, UInt32.MaxValue));
        }

        private static string CreateTestZip(string fileName, CompressionLevel compressionLevel, long maxEntrySize)
        {
            const string text = " The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog.";

            using var fs = new FileStream(fileName, FileMode.Create);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            var entrySize = maxEntrySize;
            var i = 1;

            while (entrySize > 2048)
            {
                var newEntry = archive.CreateEntry($"file{i}.txt", compressionLevel);
                using var entryStream = newEntry.Open();

                using var wr = new StreamWriter(entryStream, Encoding.UTF8, 8192);

                long written = 0;

                while (written <= entrySize)
                {
                    wr.Write(text);

                    written += text.Length;
                }

                entrySize /= 2;
                i++;
            }

            return fileName;
        }

        private async Task TestZipContent(string filePath, bool keepFile = false)
        {
            try
            {
                await using var zaStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var archive = new ZipArchive(zaStream);

                var expectedEntries = archive.Entries.ToDictionary(e => e.FullName, e => e);

                var streamCompare = new StreamCompare();

                await using var zsrStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                await using var reader = new ZipStreamReader(zsrStream);

                await foreach (var actualEntry in reader.GetEntriesAsync())
                    await using (actualEntry)
                    {
                        var expectedEntry = expectedEntries[actualEntry.FullName];

                        Assert.AreEqual(expectedEntry.FullName, actualEntry.FullName);
                        Assert.AreEqual(expectedEntry.Crc32, actualEntry.Crc32);
                        Assert.AreEqual(expectedEntry.CompressedLength, actualEntry.CompressedLength);
                        Assert.AreEqual(expectedEntry.Length, actualEntry.Length);

                        using var expectedStream = expectedEntry.Open();

                        Assert.IsTrue(await streamCompare.AreEqualAsync(expectedStream, actualEntry.Stream));
                    }
            }
            finally
            {
                if (!keepFile && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private async Task TestZipContent(Stream expectedStream, Stream actualStream)
        {
            Dictionary<string, (ZipArchiveEntry, Stream)> expectedEntries;
            using (var archive = new ZipArchive(expectedStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                expectedEntries = archive.Entries.ToDictionary(e => e.FullName, e => (e, e.Open()));
            }

            var streamCompare = new StreamCompare();

            await using var reader = new ZipStreamReader(actualStream);

            await foreach (var actualEntry in reader.GetEntriesAsync())
                await using (actualEntry)
                {
                    var (expectedEntry, ex) = expectedEntries[actualEntry.FullName];
                    using (ex)
                    {

                        Assert.AreEqual(expectedEntry.Length, actualEntry.Length);
                        Assert.AreEqual(expectedEntry.CompressedLength, actualEntry.CompressedLength);
                        Assert.AreEqual(expectedEntry.FullName, actualEntry.FullName);

                        Assert.IsTrue(await streamCompare.AreEqualAsync(ex, actualEntry.Stream));
                    }
                }
        }
    }
}