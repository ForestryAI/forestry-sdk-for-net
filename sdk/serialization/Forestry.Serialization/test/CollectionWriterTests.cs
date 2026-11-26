using System.Buffers;
using System.IO.Pipelines;
using Forestry.Serialization.Tests.Fixtures;
using Forestry.Serialization.Writing;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 004 US3: collect telemetries with a collection writer (tasks.md T032-T038), through the string
    /// test format (<see cref="TestingSerializeOptions"/>, R54)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US3's implementation (T040-T045)
    /// declares the surface they assume. For the review gate (T039), the assumed surface, in
    /// <c>Forestry.Serialization.Writing</c> (contracts/public-api.md, research R51-R53):
    /// <list type="bullet">
    /// <item><c>ICollectionWriter</c>: <c>Options</c>, <c>Sequence</c>, <c>BytesWritten</c>, <c>IsFull</c>.</item>
    /// <item><c>MonitoringCollectionStream(Stream, MonitoringCollectionOptions?)</c>, not sealed, with
    /// <c>Complete(Exception?)</c>, <c>CompleteAsync(Exception?)</c> and a <c>protected virtual
    /// OnCompleted(Exception?)</c>.</item>
    /// <item><c>MonitoringCollectionPipeWriter(PipeWriter, MonitoringCollectionOptions?)</c> and its static
    /// <c>Create(Stream, MonitoringCollectionOptions?)</c>, completed through <c>PipeWriter</c>'s own
    /// <c>Complete</c>/<c>CompleteAsync</c>.</item>
    /// <item><c>MonitoringCollectionOptions</c> with <c>init</c> members and <c>Default</c>;
    /// <c>SerializeOptions.DefaultCollectionOptions</c>.</item>
    /// </list>
    /// <c>Sequence</c> is raised by <c>TelemetrySerialization</c> after each whole telemetry (R52), so these
    /// tests serialize through it rather than writing to the collection writers directly, except where a
    /// test checks a write after completion.
    /// </remarks>
    public sealed class CollectionWriterTests
    {
        private static readonly Guid Key = new("3cdb35d7-6108-4b19-8d30-d6dfd2c44e9b");

        /// <summary>
        /// Three telemetries; the second has a name of 10 000 characters, so it takes several writes into a
        /// buffer, but is one telemetry
        /// </summary>
        private static Sample[] ThreeSamples =>
        [
            new() { Id = 1, Name = "Gran", Value = 31.4m, Key = Key },
            new() { Id = 2, Name = new string('å', 10_000), Value = 0.59m, Key = Key },
            new() { Id = 3, Name = "Tall", Value = 7m, Key = Key }
        ];

        private static TestingSerializeOptions OptionsWithSample()
        {
            TestingSerializeOptions options = new();
            options.Telemetries.Add<Sample>();
            return options;
        }

        // T032 - US3-1 / US3-2 / SC-006 (quickstart 5): Sequence counts telemetries, BytesWritten counts bytes
        [Fact]
        public void Serialize_ForThreeTelemetriesIntoACollectionStream_ItShould_CountThreeAndEveryByte()
        {
            // Arrange
            using MemoryStream memory = new();
            using MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });
            TestingSerializeOptions options = OptionsWithSample();

            // Act
            foreach (Sample sample in ThreeSamples)
            {
                TelemetrySerialization.Serialize(sample, collection, options);
            }

            // Assert
            Assert.Equal(3, collection.Sequence);
            Assert.Equal(memory.Length, collection.BytesWritten);
        }

        // T033 - US3-3 / FR-021 (quickstart 6): full after the telemetry that reaches MaxTelemetries
        [Fact]
        public void IsFull_ForMaxTelemetriesTwo_ItShould_BeFullOnlyAfterTheSecondTelemetry()
        {
            // Arrange
            using MemoryStream memory = new();
            using MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true, MaxTelemetries = 2 });
            TestingSerializeOptions options = OptionsWithSample();
            Sample[] samples = ThreeSamples;

            // Act + Assert
            Assert.False(collection.IsFull);
            TelemetrySerialization.Serialize(samples[0], collection, options);
            Assert.False(collection.IsFull);
            TelemetrySerialization.Serialize(samples[2], collection, options);
            Assert.True(collection.IsFull);
        }

        // T033 - US3-3 / FR-021: full after the telemetry that passes TargetSize, never part-way through it
        [Fact]
        public void IsFull_ForATargetSizeSmallerThanOneTelemetry_ItShould_BeFullOnlyAfterTheFirstTelemetry()
        {
            // Arrange
            using MemoryStream memory = new();
            using MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true, TargetSize = 10 });
            TestingSerializeOptions options = OptionsWithSample();

            // Act + Assert
            Assert.False(collection.IsFull);
            TelemetrySerialization.Serialize(ThreeSamples[1], collection, options);
            Assert.True(collection.IsFull);
            Assert.Equal(1, collection.Sequence);
        }

        // T034 - US3-4 / FR-022 (quickstart 7): completing without an exception is ready, after every byte arrived
        [Fact]
        public void Complete_WithoutAnException_ItShould_TellTheHookReadyAfterEveryByteArrived()
        {
            // Arrange
            using MemoryStream memory = new();
            using RecordingCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });
            TestingSerializeOptions options = OptionsWithSample();
            TelemetrySerialization.Serialize(ThreeSamples, collection, options);

            // Act
            collection.Complete();

            // Assert
            Assert.True(collection.Completed);
            Assert.Null(collection.CompletedWith);
            Assert.Equal(collection.BytesWritten, collection.InnerLengthWhenCompleted);
        }

        // T034 - US3-4 / FR-022: completing with an exception is abandoned
        [Fact]
        public void Complete_WithAnException_ItShould_TellTheHookAbandoned()
        {
            // Arrange
            using MemoryStream memory = new();
            using RecordingCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });
            IOException failure = new("The disk is full.");

            // Act
            collection.Complete(failure);

            // Assert
            Assert.True(collection.Completed);
            Assert.Same(failure, collection.CompletedWith);
        }

        // T034 - US3-4: a completed collection takes no more bytes
        [Fact]
        public void Write_AfterComplete_ItShould_Throw()
        {
            // Arrange
            using MemoryStream memory = new();
            using MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });
            collection.Complete();

            // Act + Assert
            Assert.False(collection.CanWrite);
            Assert.Throws<InvalidOperationException>(() => collection.Write("x"u8));
        }

        // T035 - US3-5 / FR-023: LeaveOpen decides whether completing disposes the inner stream
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Complete_ForLeaveOpen_ItShould_DisposeTheInnerStreamOnlyWhenNotLeftOpen(bool leaveOpen)
        {
            // Arrange
            MemoryStream memory = new();
            MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = leaveOpen });

            // Act
            collection.Complete();

            // Assert
            Assert.Equal(leaveOpen, memory.CanRead);
            memory.Dispose();
        }

        // T035 - US3-5 / FR-023: LeaveOpen decides whether completing completes the inner PipeWriter
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Complete_ForLeaveOpenOnAPipe_ItShould_CompleteTheInnerWriterOnlyWhenNotLeftOpen(bool leaveOpen)
        {
            // Arrange
            Pipe pipe = new();
            MonitoringCollectionPipeWriter collection = new(pipe.Writer, new MonitoringCollectionOptions { LeaveOpen = leaveOpen });
            await TelemetrySerialization.SerializeAsync(ThreeSamples[0], collection, OptionsWithSample());

            // Act
            await collection.CompleteAsync();

            // Assert
            Assert.True(pipe.Reader.TryRead(out ReadResult result));
            Assert.Equal(!leaveOpen, result.IsCompleted);
            pipe.Reader.AdvanceTo(result.Buffer.End);
            await pipe.Reader.CompleteAsync();
        }

        // T036 - US3-6 (quickstart 8): the collection PipeWriter counts the same as the collection stream
        [Fact]
        public async Task SerializeAsync_ForACollectionPipeWriter_ItShould_WriteAndCountTheSameAsTheCollectionStream()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream expected = new();
            using MonitoringCollectionStream stream = new(expected, new MonitoringCollectionOptions { LeaveOpen = true });
            TelemetrySerialization.Serialize(ThreeSamples, stream, options);

            Pipe pipe = new();
            MonitoringCollectionPipeWriter overPipe = new(pipe.Writer, new MonitoringCollectionOptions { LeaveOpen = true });

            // Act
            foreach (Sample sample in ThreeSamples)
            {
                await TelemetrySerialization.SerializeAsync(sample, overPipe, options);
            }

            // Assert
            Assert.Equal(stream.Sequence, overPipe.Sequence);
            Assert.Equal(stream.BytesWritten, overPipe.BytesWritten);
            Assert.True(pipe.Reader.TryRead(out ReadResult result));
            Assert.Equal(expected.ToArray(), result.Buffer.ToArray());
            pipe.Reader.AdvanceTo(result.Buffer.End);
            await pipe.Reader.CompleteAsync();
            await overPipe.CompleteAsync();
        }

        // T036 - US3-6: Create over a stream wraps the BCL's PipeWriter and counts the same
        [Fact]
        public async Task SerializeAsync_ForACollectionPipeWriterCreatedOverAStream_ItShould_WriteAndCountTheSame()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream expected = new();
            using MonitoringCollectionStream stream = new(expected, new MonitoringCollectionOptions { LeaveOpen = true });
            TelemetrySerialization.Serialize(ThreeSamples, stream, options);

            using MemoryStream memory = new();
            MonitoringCollectionPipeWriter created = MonitoringCollectionPipeWriter.Create(memory, new MonitoringCollectionOptions { LeaveOpen = true });

            // Act
            foreach (Sample sample in ThreeSamples)
            {
                await TelemetrySerialization.SerializeAsync(sample, created, options);
            }

            await created.CompleteAsync();

            // Assert
            Assert.Equal(3, created.Sequence);
            Assert.Equal(stream.BytesWritten, created.BytesWritten);
            Assert.Equal(expected.ToArray(), memory.ToArray());
        }

        // T037 - US3-7 / FR-018: a monitoring formatter's override runs
        [Fact]
        public void Serialize_ForASubclassOverridingWrite_ItShould_RunTheOverride()
        {
            // Arrange
            using MemoryStream memory = new();
            using CountingCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });

            // Act
            TelemetrySerialization.Serialize(ThreeSamples[0], collection, OptionsWithSample());

            // Assert
            Assert.True(collection.Writes > 0);
            Assert.Equal(memory.Length, collection.BytesWritten);
        }

        // T038 - R53: limits must be positive when set
        [Fact]
        public void MonitoringCollectionOptions_ForLimitsNotPositive_ItShould_Throw()
        {
            // Act + Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringCollectionOptions { TargetSize = 0 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringCollectionOptions { MaxTelemetries = -1 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonitoringCollectionOptions { MinimumBufferSize = 0 });
        }

        // T038 - R53: the default leaves the inner target to be disposed and has no limits
        [Fact]
        public void MonitoringCollectionOptions_ForDefault_ItShould_NotLeaveOpenAndHaveNoLimits()
        {
            // Arrange + Act
            MonitoringCollectionOptions defaults = MonitoringCollectionOptions.Default;

            // Assert
            Assert.False(defaults.LeaveOpen);
            Assert.Null(defaults.TargetSize);
            Assert.Null(defaults.MaxTelemetries);
            Assert.False(defaults.SizeIsCompressed);
            Assert.Equal(4096, defaults.MinimumBufferSize);
        }

        // T038 - FR-026: serialize options carry a default collection options, locked with the options
        [Fact]
        public void DefaultCollectionOptions_ForSerializeOptions_ItShould_BeTheDefaultAndLockWithTheOptions()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            MonitoringCollectionOptions custom = new() { MaxTelemetries = 1_000 };

            // Act
            Assert.Same(MonitoringCollectionOptions.Default, options.DefaultCollectionOptions);
            options.DefaultCollectionOptions = custom;
            options.MakeReadOnly();

            // Assert
            Assert.Same(custom, options.DefaultCollectionOptions);
            Assert.Throws<InvalidOperationException>(() => options.DefaultCollectionOptions = MonitoringCollectionOptions.Default);
        }
    }
}
