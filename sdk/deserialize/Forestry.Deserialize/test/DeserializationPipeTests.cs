using System.IO.Pipelines;
using System.Text;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// First end-to-end exercise of the pipe-driven walk: a real <see cref="PipeReader"/> feeding
    /// <see cref="PipeReaderBuffering"/> feeding <see cref="Deserializer{T}.TryReadValue"/> feeding
    /// <see cref="ValueAsyncEnumerable{TType, TBuffering, TStream}"/> - nothing faked below the
    /// pipe itself. A scalar root type (<see cref="string"/>) is the simplest case: no properties
    /// to walk, just one value then done.
    /// </summary>
    public class DeserializationPipeTests
    {
        [Fact]
        public async Task DeserializeAsync_ForAScalarType_ItShould_YieldExactlyOneValue()
        {
            // Arrange
            Pipe pipe = new();
            byte[] bytes = Encoding.UTF8.GetBytes("hello");
            await pipe.Writer.WriteAsync(bytes);
            await pipe.Writer.CompleteAsync();

            TestingDeserializeOptions options = new();

            // Act
            List<Value> values = new();
            await using IAsyncEnumerator<Value> enumerator = Deserialization.DeserializeAsync<string>(pipe.Reader, options);
            while (await enumerator.MoveNextAsync())
            {
                values.Add(enumerator.Current);
            }

            // Assert
            Value value = Assert.Single(values);
            Assert.Equal("hello", Encoding.UTF8.GetString(value.RawValue));
        }
    }
}
