# FastZip

FastZip is a .NET library for fast reading and streaming ZIP files. It does not use the [ZIP central directory](https://en.wikipedia.org/wiki/ZIP_%28file_format%29#Central_directory_file_header) and, thus, does not follow the specification. This makes it incompatible with multi-part ZIP archives, as well as with archives that have been updated.

### Features
 * ZIP entries can be read from a non-seekable stream on the fly. There is no need to read the stream to the end first
 * Low memory usage

### Limitations
 * Multi-part ZIP archives are not supported
 * Updated ZIP archives are not supported

### Usage
```csharp
var zipFileUrl = "https://teil-one.s3.eu-central-1.amazonaws.com/zip-mixed.zip";
using var httpClient = new HttpClient();
await using var zipStream = await httpClient.GetStreamAsync(zipFileUrl);

await using var zipStreamReader = new TeilOne.FastZip.ZipStreamReader(zipStream);

long compressedLength = 0;
long uncompressedLength = 0;
int totalEntries = 0;

await foreach (var entry in zipStreamReader.GetEntriesAsync())
{
    await using (entry)
    {
        compressedLength += entry.CompressedLength;
        uncompressedLength += entry.Length;
        totalEntries++;

        Console.Write($"\rRead {totalEntries} entries. Last entry compression method: {entry.CompressionMethod}    ");
    }
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"Compressed size: {compressedLength}, uncompressed size: {uncompressedLength}");

```
