using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Shows the shape a concrete reader is expected to ride on: implement <see cref="IReader"/>
    /// (walk forward, one <see cref="Value"/> at a time, without loading the whole media into
    /// memory), then stream it with <see cref="ValueAsyncEnumerable"/>/<see cref="ReaderExtensions.AsAsyncEnumerable"/>.
    /// </summary>
    public class ValueAsyncEnumerableTests
    {
        [Fact]
        public async Task AsAsyncEnumerable_ItShould_YieldValuesInOrderWithMetadata()
        {
            // Arrange
            TestingReader reader = new(
                () => new TestingValue("machine", "8027").WithDepth(1).WithNamespace("stanford"),
                () => new TestingValue("log", "42").WithDepth(2).WithNamespace("stanford"));

            // Act
            List<Value> values = new();
            await foreach (Value value in reader.AsAsyncEnumerable())
            {
                values.Add(value);
            }

            // Assert
            Assert.Equal(2, values.Count);

            Assert.Equal("machine", values[0].Name);
            Assert.Equal(1, values[0].Dimensions.Depth);
            Assert.Equal("stanford", values[0].Dimensions.Namespace);

            Assert.Equal("log", values[1].Name);
            Assert.Equal(2, values[1].Dimensions.Depth);
        }

        [Fact]
        public async Task AsAsyncEnumerable_WhenReaderIsEmpty_ItShould_YieldNothing()
        {
            // Arrange
            TestingReader reader = new();

            // Act
            List<Value> values = new();
            await foreach (Value value in reader.AsAsyncEnumerable())
            {
                values.Add(value);
            }

            // Assert
            Assert.Empty(values);
        }

        [Fact]
        public async Task AsAsyncEnumerable_ByDefault_ItShould_ShortCircuitOnTheFirstReadFailure()
        {
            // Arrange
            TestingReader reader = new(
                () => new TestingValue("machine", "8027"),
                () => throw new FormatException("malformed log entry"),
                () => new TestingValue("log", "42"));

            // Act
            List<Value> values = new();
            async Task EnumerateAsync()
            {
                await foreach (Value value in reader.AsAsyncEnumerable())
                {
                    values.Add(value);
                }
            }

            // Assert
            await Assert.ThrowsAsync<FormatException>(EnumerateAsync);
            Assert.Single(values); // only "machine" made it out before the failure
        }

        [Fact]
        public async Task AsAsyncEnumerable_WithShuntAside_ItShould_SkipFailedReadsAndContinue()
        {
            // Arrange
            TestingReader reader = new(
                () => new TestingValue("machine", "8027"),
                () => throw new FormatException("malformed log entry"),
                () => new TestingValue("log", "42"));

            // Act
            List<Value> values = new();
            await foreach (Value value in reader.AsAsyncEnumerable(ReadErrorHandling.ShuntAside))
            {
                values.Add(value);
            }

            // Assert
            Assert.Equal(["machine", "log"], values.Select(value => value.Name));
        }

        [Fact]
        public async Task AsAsyncEnumerable_WhenCancelled_ItShould_ThrowOperationCanceled()
        {
            // Arrange
            TestingReader reader = new(() => new TestingValue("machine", "8027"));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            // Act + Assert
            IAsyncEnumerator<Value> enumerator = reader.AsAsyncEnumerable().GetAsyncEnumerator(cancellation.Token);
            await Assert.ThrowsAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }

        [Fact(Skip =
            "Iterating types picked from a media element/attribute (e.g. all Machine or Log " +
            "entries) is future work layered on top of IReader/ValueAsyncEnumerable, not " +
            "implemented yet.")]
        public void AsAsyncEnumerable_PickingTypesByElementOrAttribute_IsNotYetImplemented()
        {
        }
    }
}
