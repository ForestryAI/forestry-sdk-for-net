using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 004 US2: serialize telemetries to a <see cref="Stream"/> or a <see cref="PipeWriter"/>
    /// (tasks.md T013-T019), through the string test format (<see cref="TestingSerializeOptions"/>, R54)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US2's implementation (T022-T030)
    /// declares the surface they assume. For the review gate (T021), the assumed surface:
    /// <list type="bullet">
    /// <item><c>SerializeOptions</c>, deriving from <c>TelemetrySerializerOptions</c> (R44), with
    /// <c>Telemetries</c> and <c>Converters</c>, and a <c>protected abstract CreateWriter()</c> returning the
    /// format's <c>TelemetryWriter</c>: protected, not protected internal, so a format package without
    /// access to the core's internals can override it (the XML package's tests link this format).</item>
    /// <item><c>TelemetryWriter</c> (R49, revised 2026-11-26): the serializer calls its
    /// <c>WriteStartTelemetry</c>, then per member <c>WriteName</c> and the converter's <c>Write</c>, or
    /// <c>WriteNull</c> for a missing value, then <c>WriteEndTelemetry</c>.</item>
    /// <item><c>TelemetrySerializer.Serialize(Telemetry | IEnumerable&lt;Telemetry&gt;, Stream, SerializeOptions)</c>,
    /// and <c>SerializeAsync</c> over a <see cref="Stream"/> or a <see cref="PipeWriter"/> (R47, R48).</item>
    /// </list>
    /// Reading a <see cref="MemoryStream"/> back: rewind, then a <see cref="StreamReader"/> that leaves it
    /// open, so the test can also check the stream was not disposed.
    /// </remarks>
    public sealed class SerializeTests
    {
        private static readonly Guid FirstKey = new("3cdb35d7-6108-4b19-8d30-d6dfd2c44e9b");

        private static readonly Guid SecondKey = new("0f8fad5b-d9cb-469f-a165-70867728950e");

        private static Sample First => new() { Id = 1, Name = "Gran", Value = 31.4m, Key = FirstKey };

        private static Sample Second => new() { Id = 2, Name = "Tall", Value = 0.59m, Key = SecondKey };

        private static string FirstLine => $"1;Gran;31.4;{FirstKey}\n";

        private static string SecondLine => $"2;Tall;0.59;{SecondKey}\n";

        public enum Target
        {
            SyncStream,
            AsyncStream,
            PipeWriter
        }

        private static TestingSerializeOptions OptionsWithSample(bool omitNulls = false, Action? onTelemetryWritten = null)
        {
            TestingSerializeOptions options = new() { OmitNulls = omitNulls, OnTelemetryWritten = onTelemetryWritten };
            options.Telemetries.Add<Sample>();
            return options;
        }

        private static string ReadBack(MemoryStream memory)
        {
            memory.Position = 0;
            using StreamReader reader = new(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return reader.ReadToEnd();
        }

        // T013 - US2-1 / quickstart 2: two telemetries, one by one, into a MemoryStream; two lines; still open
        [Fact]
        public void Serialize_ForTwoTelemetriesIntoAMemoryStream_ItShould_WriteTwoLinesAndLeaveTheStreamOpen()
        {
            // Arrange
            using MemoryStream memory = new();
            TestingSerializeOptions options = OptionsWithSample();

            // Act
            TelemetrySerializer.Serialize(First, memory, options);
            TelemetrySerializer.Serialize(Second, memory, options);

            // Assert
            Assert.True(memory.CanRead);
            Assert.Equal(FirstLine + SecondLine, ReadBack(memory));
        }

        // T014 - US2-2 / SC-003 / SC-004 (quickstart 3): the same bytes on every target; the target stays the caller's
        [Theory]
        [InlineData(Target.SyncStream)]
        [InlineData(Target.AsyncStream)]
        [InlineData(Target.PipeWriter)]
        public async Task Serialize_ForEachTarget_ItShould_WriteTheSameBytesAndNeverCloseTheTarget(Target target)
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            byte[] expected = Encoding.UTF8.GetBytes(FirstLine + SecondLine);

            // Act + Assert
            switch (target)
            {
                case Target.SyncStream:
                {
                    using MemoryStream memory = new();
                    TelemetrySerializer.Serialize(First, memory, options);
                    TelemetrySerializer.Serialize(Second, memory, options);

                    Assert.True(memory.CanRead);
                    Assert.Equal(expected, memory.ToArray());
                    break;
                }
                case Target.AsyncStream:
                {
                    using MemoryStream memory = new();
                    await TelemetrySerializer.SerializeAsync(First, memory, options);
                    await TelemetrySerializer.SerializeAsync(Second, memory, options);

                    Assert.True(memory.CanRead);
                    Assert.Equal(expected, memory.ToArray());
                    break;
                }
                case Target.PipeWriter:
                {
                    Pipe pipe = new();
                    await TelemetrySerializer.SerializeAsync(First, pipe.Writer, options);
                    await TelemetrySerializer.SerializeAsync(Second, pipe.Writer, options);

                    Assert.True(pipe.Reader.TryRead(out ReadResult result));
                    Assert.Equal(expected, result.Buffer.ToArray());
                    Assert.False(result.IsCompleted);
                    pipe.Reader.AdvanceTo(result.Buffer.End);

                    // Not completed: the caller can still write to it
                    await pipe.Writer.WriteAsync("x"u8.ToArray());
                    await pipe.Writer.CompleteAsync();
                    await pipe.Reader.CompleteAsync();
                    break;
                }
                default:
                    Assert.Fail($"No case for target {target}");
                    break;
            }
        }

        // T015 - US2-3 / FR-011: many telemetries in one call, in order, taken one at a time
        [Fact]
        public void Serialize_ForASequence_ItShould_TakeEachTelemetryOnlyAfterThePreviousOneWasWritten()
        {
            // Arrange
            List<string> events = [];
            TestingSerializeOptions options = OptionsWithSample(onTelemetryWritten: () => events.Add("written"));
            using MemoryStream memory = new();

            // Act
            TelemetrySerializer.Serialize(Pulled(events, First, Second), memory, options);

            // Assert
            Assert.Equal(["pulled 1", "written", "pulled 2", "written"], events);
            Assert.Equal(FirstLine + SecondLine, ReadBack(memory));
        }

        // T015 - US2-3 / FR-011: the asynchronous sequence, the same way
        [Fact]
        public async Task SerializeAsync_ForAnAsyncSequence_ItShould_TakeEachTelemetryOnlyAfterThePreviousOneWasWritten()
        {
            // Arrange
            List<string> events = [];
            TestingSerializeOptions options = OptionsWithSample(onTelemetryWritten: () => events.Add("written"));
            using MemoryStream memory = new();

            // Act
            await TelemetrySerializer.SerializeAsync(PulledAsync(events, First, Second), memory, options);

            // Assert
            Assert.Equal(["pulled 1", "written", "pulled 2", "written"], events);
            Assert.Equal(FirstLine + SecondLine, ReadBack(memory));
        }

        // T016 - US2-5 / R50: a null member is written through the writer's WriteNull; the converter never sees null
        [Fact]
        public void Serialize_ForANullMember_ItShould_WriteTheNullSectionWithoutCallingTheConverter()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            options.Converters.Add(new NullRefusingStringConverter());
            using MemoryStream memory = new();

            // Act
            TelemetrySerializer.Serialize(new Sample { Id = 1, Name = null, Value = 31.4m, Key = FirstKey }, memory, options);

            // Assert
            Assert.Equal($"1;;31.4;{FirstKey}\n", ReadBack(memory));
        }

        // T016 - R49: a writer that omits nulls leaves the member out, with no stray separator
        [Fact]
        public void Serialize_ForANullMemberAndAFormatOmittingNulls_ItShould_LeaveTheMemberOut()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample(omitNulls: true);
            using MemoryStream memory = new();

            // Act
            TelemetrySerializer.Serialize(new Sample { Id = 1, Name = null, Value = 31.4m, Key = FirstKey }, memory, options);

            // Assert
            Assert.Equal($"1;31.4;{FirstKey}\n", ReadBack(memory));
        }

        // T016 - R49: the separator comes only after a written member, so an omitted first member leaves none
        [Theory]
        [InlineData(false, ";7\n")]
        [InlineData(true, "7\n")]
        public void Serialize_ForANullFirstMember_ItShould_WriteNoStraySeparator(bool omitNulls, string expected)
        {
            // Arrange
            TestingSerializeOptions options = new() { OmitNulls = omitNulls };
            options.Telemetries.Add<OptionalSample>();
            using MemoryStream memory = new();

            // Act
            TelemetrySerializer.Serialize(new OptionalSample { Note = null, Id = 7 }, memory, options);

            // Assert
            Assert.Equal(expected, ReadBack(memory));
        }

        // T017 - US2-6 / FR-008 / SC-007: a member whose converter cannot write fails at lock, before any byte
        [Fact]
        public void Serialize_ForAMemberWhoseConverterOnlyReads_ItShould_FailNamingTypeAndMemberBeforeWriting()
        {
            // Arrange
            TestingSerializeOptions options = new();
            options.Converters.Add(new SpeciesReadingConverter());
            options.Telemetries.Add<TreeWithSpecies>();
            using MemoryStream memory = new();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Serialize(new TreeWithSpecies { Id = 1, Species = new Species(3) }, memory, options));

            // Assert
            Assert.Contains(nameof(TreeWithSpecies), exception.Message);
            Assert.Contains(nameof(TreeWithSpecies.Species), exception.Message);
            Assert.Equal(0, memory.Length);
        }

        // T018 - US2-7 / FR-017: a telemetry whose type was not added fails naming the type, before any byte
        [Fact]
        public void Serialize_ForATelemetryTypeNotAdded_ItShould_FailNamingTheTypeBeforeWriting()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream memory = new();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerializer.Serialize(new OptionalSample { Id = 7 }, memory, options));

            // Assert
            Assert.Contains(nameof(OptionalSample), exception.Message);
            Assert.Equal(0, memory.Length);
        }

        // T019 - edge case: an empty sequence writes nothing
        [Fact]
        public void Serialize_ForAnEmptySequence_ItShould_WriteNothing()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream memory = new();

            // Act
            TelemetrySerializer.Serialize(Array.Empty<Telemetry>(), memory, options);

            // Assert
            Assert.Equal(0, memory.Length);
        }

        // T019 - edge case: cancellation stops before the next telemetry and leaves the stream open
        [Fact]
        public async Task SerializeAsync_ForACancellationAfterTheFirstTelemetry_ItShould_StopBeforeTheNextAndLeaveTheStreamOpen()
        {
            // Arrange
            using CancellationTokenSource cancellation = new();
            TestingSerializeOptions options = OptionsWithSample(onTelemetryWritten: cancellation.Cancel);
            using MemoryStream memory = new();

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => TelemetrySerializer.SerializeAsync(PulledAsync([], First, Second), memory, options, cancellation.Token));

            // Assert
            Assert.True(memory.CanRead);
            Assert.DoesNotContain(SecondLine, ReadBack(memory));
        }

        // T019 - edge case: a stream that cannot be written fails before anything is written
        [Fact]
        public void Serialize_ForAReadOnlyStream_ItShould_FailBeforeWriting()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream readOnly = new([], writable: false);

            // Act + Assert
            Assert.Throws<ArgumentException>(() => TelemetrySerializer.Serialize(First, readOnly, options));
        }

        // T019 - edge case: the options lock on first use, as deserialize options do
        [Fact]
        public void Serialize_ForOptionsChangedAfterTheFirstSerialize_ItShould_Throw()
        {
            // Arrange
            TestingSerializeOptions options = OptionsWithSample();
            using MemoryStream memory = new();
            TelemetrySerializer.Serialize(First, memory, options);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Telemetries.Add<OptionalSample>());
        }

        /// <summary>
        /// Hand out <paramref name="telemetries"/> one at a time, recording each pull in <paramref name="events"/>
        /// </summary>
        private static IEnumerable<Telemetry> Pulled(List<string> events, params Sample[] telemetries)
        {
            foreach (Sample telemetry in telemetries)
            {
                events.Add($"pulled {telemetry.Id}");
                yield return telemetry;
            }
        }

        /// <summary>
        /// As <see cref="Pulled"/>, asynchronously
        /// </summary>
        private static async IAsyncEnumerable<Telemetry> PulledAsync(List<string> events, Sample first, Sample second, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (Sample telemetry in new[] { first, second })
            {
                await Task.Yield();
                events.Add($"pulled {telemetry.Id}");
                yield return telemetry;
            }
        }
    }
}
