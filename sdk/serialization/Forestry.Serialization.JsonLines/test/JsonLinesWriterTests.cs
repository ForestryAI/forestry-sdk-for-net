using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Forestry.Serialization.JsonLines.Tests.Fixtures;
using Forestry.Serialization.Writing;
using Xunit;

namespace Forestry.Serialization.JsonLines.Tests
{
    /// <summary>
    /// Spec 005 US3 (#151): telemetries written as JSON Lines (tasks.md T037-T044; research R65, R66)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US3's implementation (T046-T049) declares
    /// the surface they assume. For the review gate (T045), the assumed surface, in namespace
    /// <c>Forestry.Serialization.JsonLines</c>:
    /// <list type="bullet">
    /// <item><c>SerializeJsonLinesOptions</c>, a sealed <c>SerializeOptions</c>: one JSON object per telemetry
    /// and a <c>\n</c> after it, members by their C# names in definition order, over <c>Utf8JsonWriter</c> with
    /// relaxed escaping (non-ASCII text as plain UTF-8).</item>
    /// <item>Text, <see cref="Guid"/>, dates, times, durations and enum names as JSON strings; numbers and
    /// booleans bare; a missing value as <c>null</c>.</item>
    /// <item><c>[JsonLinesEnumForm(EnumForm.Number)]</c> on a telemetry member writes that member's enum as a
    /// number; names by default.</item>
    /// <item>A NaN or infinite <see cref="double"/>, and a converter using the writer's
    /// <see cref="IBufferWriter{T}"/> members, throw <see cref="NotSupportedException"/> naming the member;
    /// <c>WriteRaw</c> takes a complete JSON value only.</item>
    /// </list>
    /// Lines are parsed with <see cref="JsonDocument"/>; dates, times and durations are read back from their
    /// strings with the invariant culture.
    /// </remarks>
    public sealed class JsonLinesWriterTests
    {
        public enum Target
        {
            SyncStream,
            AsyncStream,
            PipeWriter
        }

        private static readonly string[] s_memberOrder =
        [
            "Text", "Flag", "Count", "Big", "Ratio", "Volume", "Key", "At", "AtOffset", "Day", "Time", "Duration",
            "Shade", "ShadeNumber", "MaybeCount", "MaybeDay"
        ];

        // T037 - US3-1 / FR-009: one complete JSON object per line, members in definition order
        [Fact]
        public void Serialize_ForThreeTelemetries_ItShould_WriteThreeLinesOfOneObjectEach()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();

            // Act
            string[] lines = JsonLinesOutput.Lines(options, EveryKindTelemetry.WithValues(1), EveryKindTelemetry.WithValues(2), EveryKindTelemetry.WithValues(3));

            // Assert
            Assert.Equal(3, lines.Length);

            for (int index = 0; index < lines.Length; index++)
            {
                using JsonDocument line = JsonDocument.Parse(lines[index]);
                Assert.Equal(JsonValueKind.Object, line.RootElement.ValueKind);
                Assert.Equal(s_memberOrder, line.RootElement.EnumerateObject().Select(property => property.Name));
                Assert.Equal(index + 1, line.RootElement.GetProperty("Count").GetInt32());
            }
        }

        // T038 - US3-2 / FR-011: each kind is its JSON kind with the member's value
        [Fact]
        public void Serialize_ForEveryMemberKind_ItShould_WriteEachAsItsJsonKindWithItsValue()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();
            EveryKindTelemetry telemetry = EveryKindTelemetry.WithValues();

            // Act
            using JsonDocument line = JsonDocument.Parse(JsonLinesOutput.Lines(options, telemetry)[0]);

            // Assert
            JsonElement root = line.RootElement;
            Assert.Equal(telemetry.Text, root.GetProperty("Text").GetString());
            Assert.Equal(JsonValueKind.True, root.GetProperty("Flag").ValueKind);
            Assert.Equal(telemetry.Count, root.GetProperty("Count").GetInt32());
            Assert.Equal(telemetry.Big, root.GetProperty("Big").GetInt64());
            Assert.Equal(telemetry.Ratio, root.GetProperty("Ratio").GetDouble());
            Assert.Equal(telemetry.Volume, root.GetProperty("Volume").GetDecimal());
            Assert.Equal(telemetry.Key, root.GetProperty("Key").GetGuid());
            Assert.Equal(telemetry.At, root.GetProperty("At").GetDateTime());
            Assert.Equal(telemetry.AtOffset, root.GetProperty("AtOffset").GetDateTimeOffset());
            Assert.Equal(telemetry.Day, DateOnly.Parse(root.GetProperty("Day").GetString()!, CultureInfo.InvariantCulture));
            Assert.Equal(telemetry.Time, TimeOnly.Parse(root.GetProperty("Time").GetString()!, CultureInfo.InvariantCulture));
            Assert.Equal(telemetry.Duration, TimeSpan.Parse(root.GetProperty("Duration").GetString()!, CultureInfo.InvariantCulture));
            Assert.Equal("Dark", root.GetProperty("Shade").GetString());
            Assert.Equal(telemetry.MaybeCount, root.GetProperty("MaybeCount").GetInt32());
            Assert.Equal(telemetry.MaybeDay, DateOnly.Parse(root.GetProperty("MaybeDay").GetString()!, CultureInfo.InvariantCulture));
        }

        // T038 - US3-2 / FR-011: a member without a value is null
        [Fact]
        public void Serialize_ForMembersWithoutValues_ItShould_WriteNull()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();

            // Act
            using JsonDocument line = JsonDocument.Parse(JsonLinesOutput.Lines(options, EveryKindTelemetry.WithNulls())[0]);

            // Assert
            JsonElement root = line.RootElement;
            Assert.Equal(JsonValueKind.Null, root.GetProperty("Text").ValueKind);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("MaybeCount").ValueKind);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("MaybeDay").ValueKind);
        }

        // T039 - US3-3 / SC-005: JSON-special text stays on one line, non-ASCII as plain UTF-8, and reads back exactly
        [Fact]
        public void Serialize_ForTextWithJsonSpecialCharacters_ItShould_StayOneLineAndReadBackExactly()
        {
            // Arrange
            const string Text = "quote \" backslash \\ control \u0001 newline \n crlf \r\n åäö tree \U0001F332";
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<TextTelemetry>();

            // Act
            byte[] bytes = JsonLinesOutput.Bytes(options, new TextTelemetry { Text = Text });

            // Assert
            string[] lines = JsonLinesOutput.Lines(bytes);
            Assert.Single(lines);
            Assert.True(bytes.AsSpan().IndexOf("åäö"u8) >= 0, "åäö is written as plain UTF-8");
            using JsonDocument line = JsonDocument.Parse(lines[0]);
            Assert.Equal(Text, line.RootElement.GetProperty("Text").GetString());
        }

        // T040 - US3-4 / C8: every target writes the same bytes and stays the caller's
        [Theory]
        [InlineData(Target.SyncStream)]
        [InlineData(Target.AsyncStream)]
        [InlineData(Target.PipeWriter)]
        public async Task Serialize_ForEachTarget_ItShould_WriteTheSameBytesAndNeverCloseTheTarget(Target target)
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();
            EveryKindTelemetry first = EveryKindTelemetry.WithValues(1);
            EveryKindTelemetry second = EveryKindTelemetry.WithNulls();
            byte[] expected = JsonLinesOutput.Bytes(options, first, second);

            // Act + Assert
            switch (target)
            {
                case Target.SyncStream:
                {
                    using MemoryStream memory = new();
                    TelemetrySerialization.Serialize(first, memory, options);
                    TelemetrySerialization.Serialize(second, memory, options);

                    Assert.True(memory.CanRead);
                    Assert.Equal(expected, memory.ToArray());
                    break;
                }
                case Target.AsyncStream:
                {
                    using MemoryStream memory = new();
                    await TelemetrySerialization.SerializeAsync(first, memory, options);
                    await TelemetrySerialization.SerializeAsync(second, memory, options);

                    Assert.True(memory.CanRead);
                    Assert.Equal(expected, memory.ToArray());
                    break;
                }
                case Target.PipeWriter:
                {
                    Pipe pipe = new();
                    await TelemetrySerialization.SerializeAsync(first, pipe.Writer, options);
                    await TelemetrySerialization.SerializeAsync(second, pipe.Writer, options);

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

        // T041 - US3-5 / FR-013: in a collection writer, the sequence counts the lines
        [Fact]
        public void Serialize_IntoACollectionStream_ItShould_CountOneSequencePerLine()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();
            using MemoryStream memory = new();
            using MonitoringCollectionStream collection = new(memory, new MonitoringCollectionOptions { LeaveOpen = true });

            // Act
            TelemetrySerialization.Serialize(EveryKindTelemetry.WithValues(1), collection, options);
            TelemetrySerialization.Serialize(EveryKindTelemetry.WithValues(2), collection, options);
            TelemetrySerialization.Serialize(EveryKindTelemetry.WithValues(3), collection, options);
            collection.Flush();

            // Assert
            Assert.Equal(3, collection.Sequence);
            Assert.Equal(memory.Length, collection.BytesWritten);
            Assert.Equal(3, JsonLinesOutput.Lines(memory.ToArray()).Length);
        }

        // T042 - US3-6 / FR-002d: names by default; a member marked for numbers is a number, per member
        [Fact]
        public void Serialize_ForTwoMembersOfOneEnumTypeOneMarkedForNumbers_ItShould_WriteEachInItsOwnForm()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<EveryKindTelemetry>();

            // Act
            using JsonDocument line = JsonDocument.Parse(JsonLinesOutput.Lines(options, EveryKindTelemetry.WithValues())[0]);

            // Assert
            JsonElement byName = line.RootElement.GetProperty("Shade");
            JsonElement byNumber = line.RootElement.GetProperty("ShadeNumber");
            Assert.Equal(JsonValueKind.String, byName.ValueKind);
            Assert.Equal("Dark", byName.GetString());
            Assert.Equal(JsonValueKind.Number, byNumber.ValueKind);
            Assert.Equal(1, byNumber.GetInt32());
        }

        // T043 - US3-7 / FR-011a: a NaN or infinite double is refused, naming the member and the value
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Serialize_ForANonFiniteDouble_ItShould_ThrowNamingTheMemberAndTheValue(double ratio)
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<RatioTelemetry>();
            using MemoryStream memory = new();

            // Act
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
                TelemetrySerialization.Serialize(new RatioTelemetry { Ratio = ratio }, memory, options));

            // Assert
            Assert.Contains(nameof(RatioTelemetry.Ratio), exception.Message, StringComparison.Ordinal);
            Assert.Contains(ratio.ToString(CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        }

        // T044 - US3-8 / FR-011b: a converter writing through the IBufferWriter members is refused
        [Fact]
        public void Serialize_ForAConverterWritingThroughTheBufferMembers_ItShould_ThrowSayingToUseTheValueMethods()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<CountTelemetry>(new DirectBufferInt32Converter());
            using MemoryStream memory = new();

            // Act
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
                TelemetrySerialization.Serialize(new CountTelemetry { Count = 1 }, memory, options));

            // Assert
            Assert.Contains(nameof(TelemetryWriter.WriteValue), exception.Message, StringComparison.Ordinal);
        }

        // T044 - FR-011b: WriteRaw writes a complete JSON value as it is
        [Fact]
        public void Serialize_ForAConverterWritingACompleteRawValue_ItShould_WriteIt()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<CountTelemetry>(new RawInt32Converter("{\"a\":1}"));

            // Act
            using JsonDocument line = JsonDocument.Parse(JsonLinesOutput.Lines(options, new CountTelemetry())[0]);

            // Assert
            Assert.Equal(1, line.RootElement.GetProperty("Count").GetProperty("a").GetInt32());
        }

        // T044 - FR-011b: WriteRaw refuses an incomplete JSON value, never writing an invalid line
        [Fact]
        public void Serialize_ForAConverterWritingAnIncompleteRawValue_ItShould_Throw()
        {
            // Arrange
            SerializeJsonLinesOptions options = JsonLinesOutput.OptionsWith<CountTelemetry>(new RawInt32Converter("{"));
            using MemoryStream memory = new();

            // Act + Assert
            Assert.ThrowsAny<Exception>(() => TelemetrySerialization.Serialize(new CountTelemetry(), memory, options));
        }
    }
}
