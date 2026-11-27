using System.Buffers;
using System.Globalization;
using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Forestry.Serialization.Writing;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 005 US1: built-in converters for <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="TimeSpan"/>
    /// and enums, and the telemetry writer's methods for them (tasks.md T003-T006; research R60, R61)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US1's implementation (T014-T022) declares
    /// the surface they assume. For the review gate (T013), the assumed surface:
    /// <list type="bullet">
    /// <item><c>TelemetryWriter.WriteValue(DateOnly)</c> and <c>WriteValue(TimeOnly)</c> (default <c>O</c>),
    /// <c>WriteValue(TimeSpan)</c> (default <c>c</c>) and <c>WriteEnumName(ReadOnlySpan&lt;byte&gt;)</c> (R60).</item>
    /// <item>Built-in converters for the three types and for enums, resolved with nothing registered, through
    /// <see cref="ConverterRoundTrip.BuiltIn{T}"/> (R61, R59).</item>
    /// <item>Reading is lenient: <c>13:20:00</c> and <c>13:20:00.5</c> are times, as XML Schema writes them (R61).</item>
    /// </list>
    /// </remarks>
    public sealed class BuiltInConverterTests
    {
        private static readonly DateOnly Date = new(2026, 11, 27);

        private static readonly TimeOnly Time = new(13, 20, 0);

        private static readonly TimeSpan Duration = new(1, 2, 3);

        private static string ReadBack(MemoryStream memory)
        {
            memory.Position = 0;
            using StreamReader reader = new(memory, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public static TheoryData<DateOnly> Dates => [DateOnly.MinValue, DateOnly.MaxValue, Date];

        public static TheoryData<TimeOnly> Times => [TimeOnly.MinValue, TimeOnly.MaxValue, new TimeOnly(13, 20, 0).Add(TimeSpan.FromTicks(1_234_567))];

        public static TheoryData<TimeSpan> Durations => [TimeSpan.Zero, TimeSpan.MinValue, TimeSpan.MaxValue, -new TimeSpan(1, 2, 3, 4, 500)];

        // T003 - US1-1 / SC-001 (quickstart 1): the new member types build for both directions with nothing registered
        [Theory]
        [InlineData(typeof(TimedTelemetry), false)]
        [InlineData(typeof(TimedTelemetry), true)]
        [InlineData(typeof(EnumTelemetry), false)]
        [InlineData(typeof(EnumTelemetry), true)]
        public void Definitions_ForDateTimeDurationAndEnumMembers_ItShould_ResolveABuiltInConverterForEachMember(Type telemetryType, bool serialize)
        {
            // Arrange
            TelemetrySerializationOptions options = serialize ? new TestingSerializeOptions() : new TestingDeserializeOptions();
            options.Telemetries.Add(telemetryType);

            // Act
            options.MakeReadOnly();

            // Assert
            Assert.True(options.TryGetTypeDefinition(telemetryType, out TypeDefinition? definition));
            Assert.All(definition!.Properties, property => Assert.NotNull(property.ResolvedConverter));
        }

        // T004 - US1-2 / SC-002: dates write and read back the same, boundaries included
        [Theory]
        [MemberData(nameof(Dates))]
        public void BuiltInConverter_ForADateOnly_ItShould_ReadBackTheValueItWrote(DateOnly value)
        {
            // Arrange
            Converter<DateOnly> converter = ConverterRoundTrip.BuiltIn<DateOnly>();

            // Act
            DateOnly read = ConverterRoundTrip.RoundTrip(converter, value);

            // Assert
            Assert.Equal(value, read);
        }

        // T004 - US1-2 / SC-002: times write and read back the same, to the tick
        [Theory]
        [MemberData(nameof(Times))]
        public void BuiltInConverter_ForATimeOnly_ItShould_ReadBackTheValueItWrote(TimeOnly value)
        {
            // Arrange
            Converter<TimeOnly> converter = ConverterRoundTrip.BuiltIn<TimeOnly>();

            // Act
            TimeOnly read = ConverterRoundTrip.RoundTrip(converter, value);

            // Assert
            Assert.Equal(value, read);
        }

        // T004 - US1-2 / SC-002: durations write and read back the same, negative and boundaries included
        [Theory]
        [MemberData(nameof(Durations))]
        public void BuiltInConverter_ForATimeSpan_ItShould_ReadBackTheValueItWrote(TimeSpan value)
        {
            // Arrange
            Converter<TimeSpan> converter = ConverterRoundTrip.BuiltIn<TimeSpan>();

            // Act
            TimeSpan read = ConverterRoundTrip.RoundTrip(converter, value);

            // Assert
            Assert.Equal(value, read);
        }

        // T004 - R61: reading is lenient, as XML Schema writes times without or with fewer fractions
        [Theory]
        [InlineData("13:20:00", 0)]
        [InlineData("13:20:00.5", 5_000_000)]
        public void BuiltInConverter_ForATimeWithoutAllFractions_ItShould_ReadIt(string text, long fractionTicks)
        {
            // Arrange
            Converter<TimeOnly> converter = ConverterRoundTrip.BuiltIn<TimeOnly>();

            // Act
            TimeOnly read = converter.Read(Encoding.UTF8.GetBytes(text));

            // Assert
            Assert.Equal(Time.Add(TimeSpan.FromTicks(fractionTicks)), read);
        }

        // T004 - R61: malformed text fails as the other built-in converters do
        [Fact]
        public void BuiltInConverter_ForMalformedDateText_ItShould_ThrowAFormatException()
        {
            // Arrange
            Converter<DateOnly> converter = ConverterRoundTrip.BuiltIn<DateOnly>();

            // Act + Assert
            Assert.Throws<FormatException>(() => converter.Read("27/11/2026 or so"u8));
        }

        // T005 - US1-3 / FR-004 (quickstart 2): each new kind goes to its own writer method
        [Fact]
        public void BuiltInConverters_ForTheNewKinds_ItShould_CallEachKindsWriterMethod()
        {
            // Arrange
            ArrayBufferWriter<byte> output = new();
            TypedTelemetryWriter writer = TypedTelemetryWriter.Over(output);

            // Act
            ConverterRoundTrip.BuiltIn<DateOnly>().Write(Date, writer);
            ConverterRoundTrip.BuiltIn<TimeOnly>().Write(Time, writer);
            ConverterRoundTrip.BuiltIn<TimeSpan>().Write(Duration, writer);
            ConverterRoundTrip.BuiltIn<Shade>().Write(Shade.Dark, writer);

            // Assert
            Assert.Equal(
                "DateOnly:2026" + "TimeOnly:13" + "TimeSpan:" + Duration.Ticks.ToString(CultureInfo.InvariantCulture) + "enum:Dark",
                Encoding.UTF8.GetString(output.WrittenSpan));
        }

        // T005 - US1-3 / FR-004: overriding one new method changes that kind only
        [Fact]
        public void TelemetryWriter_OverridingOnlyTheTimeSpanMethod_ItShould_ChangeTimeSpansAndNothingElse()
        {
            // Arrange
            ArrayBufferWriter<byte> output = new();
            MinutesTelemetryWriter writer = MinutesTelemetryWriter.Over(output);

            // Act
            ConverterRoundTrip.BuiltIn<TimeSpan>().Write(Duration, writer);
            ConverterRoundTrip.BuiltIn<DateOnly>().Write(Date, writer);

            // Assert
            Assert.Equal("62min" + "2026-11-27", Encoding.UTF8.GetString(output.WrittenSpan));
        }

        // T006 - US1-4: nullable dates and durations write their value, or the format's null
        [Fact]
        public void Serialize_ForNullableDateAndDurationWithAndWithoutValues_ItShould_WriteTheValueOrTheFormatsNull()
        {
            // Arrange
            using MemoryStream memory = new();
            TestingSerializeOptions options = new();
            options.Telemetries.Add<TimedTelemetry>();
            TimedTelemetry withValues = new() { Date = Date, Time = Time, Duration = Duration, MaybeDate = Date.AddDays(1), MaybeDuration = TimeSpan.FromSeconds(5) };
            TimedTelemetry withoutValues = new() { Date = Date, Time = Time, Duration = Duration };

            // Act
            TelemetrySerialization.Serialize(withValues, memory, options);
            TelemetrySerialization.Serialize(withoutValues, memory, options);

            // Assert
            Assert.Equal(
                "2026-11-27;13:20:00.0000000;01:02:03;2026-11-28;00:00:05\n" +
                "2026-11-27;13:20:00.0000000;01:02:03;;\n",
                ReadBack(memory));
        }

        /// <summary>
        /// A writer overriding only <c>WriteValue(TimeSpan)</c>, writing whole minutes
        /// </summary>
        private sealed class MinutesTelemetryWriter : TelemetryWriter
        {
            public static MinutesTelemetryWriter Over(IBufferWriter<byte> output)
            {
                MinutesTelemetryWriter writer = new();
                writer.Reset(output);
                return writer;
            }

            public override void WriteValue(TimeSpan value) =>
                WriteRaw(Encoding.UTF8.GetBytes(((int)value.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "min"));
        }
    }
}
