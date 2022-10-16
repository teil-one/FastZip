using CommunityToolkit.HighPerformance;
using NeoSmart.StreamCompare;

namespace TeilOne.FastZip.Tests
{
    [TestClass]
    public class BaseStreamReaderTests
    {
        [TestMethod]
        [Timeout(100)]
        public async Task Constructor_BufferSizeTooSmallForSearch_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async () =>
            {
                await using var reader = new BaseStreamReader(dataStream, 4, 3);
            });
        }

        [TestMethod]
        [Timeout(100)]
        public async Task CompactBuffer_ShiftsBufferLeft()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            // Act & Assert
            var read1 = await reader.ReadNextAsync(2);
            Assert.IsTrue(data.AsSpan(0, 2).SequenceEqual(buffer.AsSpan(0, 2)));

            reader.CompactBuffer();
            await reader.ReadMoreAsync();
            Assert.IsTrue(data.AsSpan(2, 1).SequenceEqual(buffer.AsSpan(0, 1)));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadMoreAsync_BufferIsFull_ThrowsInvalidOperationException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 4, 5);

            await reader.ReadNextAsync(5);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await reader.ReadMoreAsync());
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadMoreAsync_BufferIsNotFull_ReadsStream()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            // Act
            var bytesRead = await reader.ReadMoreAsync();

            // Assert
            Assert.IsTrue(bytesRead > 0);
            Assert.IsTrue(data.AsSpan(0, bytesRead).SequenceEqual(buffer.AsSpan(0, bytesRead)));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadNextAsync_CountBiggerThanBuffer_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 4, 7);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async () => await reader.ReadNextAsync(10));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadNextAsync_CountBiggerThanStream_ThrowsInvalidOperationException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 4, 9);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await reader.ReadNextAsync(9));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadNextAsync_ReturnsCorrectData()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 4, 5);

            // Act
            var read1 = await reader.ReadNextAsync(2);
            var read2 = await reader.ReadNextAsync(2);
            var read3 = await reader.ReadNextAsync(2);
            var read4 = await reader.ReadNextAsync(2);

            // Assert
            Assert.IsTrue(data.AsSpan(0, 2).SequenceEqual(read1));
            Assert.IsTrue(data.AsSpan(2, 2).SequenceEqual(read2));
            Assert.IsTrue(data.AsSpan(4, 2).SequenceEqual(read3));
            Assert.IsTrue(data.AsSpan(6, 2).SequenceEqual(read4));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task GotoNext_SearchLongerThanAllowed_ThrowsArgumentException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 3, 9);

            var search = new byte[] { 0, 1, 2, 3 };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await reader.GotoNext(search));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task GotoNext_SearchResultNotFound_ReturnsFalse()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            var search = new byte[] { 0, 1, 2, 4 };

            // Act
            var found = await reader.GotoNext(search);

            // Assert
            Assert.IsFalse(found);
            Assert.IsTrue(buffer.SequenceEqual(new byte[] { 5, 6, 7, 7 }));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task GotoNext_CalledInSequence_ReturnsCorrectResults()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            // Act & Assert
            Assert.IsTrue(await reader.GotoNext(new byte[] { 0, 1, 2, 3 }));

            Assert.IsTrue(await reader.GotoNext(new byte[] { 3, 4 }));

            Assert.IsTrue(await reader.GotoNext(new byte[] { 5 }));

            Assert.IsTrue(await reader.GotoNext(new byte[] { 6, 7 }));

            Assert.IsTrue(await reader.GotoNext(new byte[] { 7 }));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadUntil_SearchLongerThanAllowed_ThrowsArgumentException()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);

            await using var reader = new BaseStreamReader(dataStream, 4, 10);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await reader.ReadUntil(
                new byte[][]
                {
                    new byte[] { 2, 2, 4, 5 },
                    new byte[] { 2, 2, 4, 5, 6 }
                }));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadUntil_NotFound_ReturnsNull()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            // Act
            var read = await reader.ReadUntil(new byte[][] { new byte[] { 2, 2, 4, 5 } });

            // Assert
            Assert.IsNull(read);
            Assert.IsTrue(buffer.SequenceEqual(new byte[] { 5, 6, 7, 7 }));
        }

        [TestMethod]
        [Timeout(100)]
        public async Task ReadUntil_CalledInSequence_ReturnsCorrectResults()
        {
            // Arrange
            var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            await using var dataStream = GetStream(data);
            var buffer = new byte[4];

            await using var reader = new BaseStreamReader(dataStream, 4, buffer);

            var streamCompare = new StreamCompare();

            // Act & Assert
            var read = await reader.ReadUntil(new byte[][] { new byte[] { 2, 3, 4, 5 } });
            Assert.IsNotNull(read);
            Assert.IsTrue(await streamCompare.AreEqualAsync(new byte[] { 0, 1 }.AsMemory().AsStream(), read));

            read = await reader.ReadUntil(new byte[][] { new byte[] { 2, 3 } });
            Assert.IsNotNull(read);
            Assert.AreEqual(0, read.Length);

            read = await reader.ReadUntil(new byte[][] { new byte[] { 3, 4 } });
            Assert.IsNotNull(read);
            Assert.IsTrue(await streamCompare.AreEqualAsync(new byte[] { 2 }.AsMemory().AsStream(), read));

            read = await reader.ReadUntil(new byte[][] { new byte[] { 7 } });
            Assert.IsNotNull(read);
            Assert.IsTrue(await streamCompare.AreEqualAsync(new byte[] { 3, 4, 5, 6 }.AsMemory().AsStream(), read));
        }

        private Stream GetStream(byte[] data)
        {
            return new MemoryStream(data);
        }
    }
}
