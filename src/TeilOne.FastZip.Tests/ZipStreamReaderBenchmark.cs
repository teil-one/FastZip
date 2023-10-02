using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using System.IO.Compression;

namespace TeilOne.FastZip.Tests
{
    [TestClass]
    public class ZipStreamReaderBenchmark
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ZipStreamReader_vs_ZipArchive()
        {
            var logger = new AccumulationLogger();
            var config = ManualConfig.Create(DefaultConfig.Instance)
                        .AddLogger(logger)
                        .WithOptions(ConfigOptions.DisableOptimizationsValidator);

            BenchmarkRunner.Run<Benchmark>(config);

            // write benchmark summary
            TestContext.WriteLine(logger.GetLog());
        }

        [TestMethod]
        public async Task ReadWithZipStreamReader()
        {
            var benchmark = new Benchmark();

            try
            {
                benchmark.Setup();

                await benchmark.ReadWithZipStreamReader();
            }
            finally
            {
                benchmark.Cleanup();
            }
        }
    }

    [MemoryDiagnoser]
    public class Benchmark
    {
        private string _file;

        [GlobalSetup]
        public void Setup()
        {
            _file = TestHelper.CreateTestZip(
                "zip-store.zip",
                CompressionLevel.NoCompression,
                256 * 1024 * 1024);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (File.Exists(_file))
            {
                File.Delete(_file);
            }
        }

        [Benchmark]
        public async Task ReadWithZipStreamReader()
        {
            var filePath = _file;

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await using var reader = new ZipStreamReader(fileStream);

            var buffer = new Memory<byte>(new byte[256 * 1024]);

            await foreach (var entry in reader.GetEntriesAsync())
                await using (entry)
                {
                    var readBytes = 0;
                    do
                    {
                        readBytes = await entry.Stream.ReadAsync(buffer);
                    }
                    while (readBytes > 0);
                }
        }

        [Benchmark]
        public async Task ReadWithZipArchive()
        {
            var filePath = _file;

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(fileStream);

            var buffer = new Memory<byte>(new byte[256 * 1024]);

            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                var readBytes = 0;
                do
                {
                    readBytes = await entryStream.ReadAsync(buffer);
                }
                while (readBytes > 0);
            }
        }
    }
}