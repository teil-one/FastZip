using System.IO.Compression;
using System.Text;

namespace TeilOne.FastZip.Tests
{
    internal static class TestHelper
    {
        public static string CreateTestZip(string fileName, CompressionLevel compressionLevel, long maxEntrySize)
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
    }
}
