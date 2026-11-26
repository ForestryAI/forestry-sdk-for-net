using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 004 US1: converters read and write (tasks.md T002-T005, research R45)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US1's implementation (T007-T010)
    /// declares the surface they assume. For the review gate (T006), the assumed surface:
    /// <list type="bullet">
    /// <item><c>Converter&lt;T&gt;.Read(ReadOnlySpan&lt;byte&gt;)</c>, renamed from <c>Convert</c> (R56).</item>
    /// <item><c>Converter&lt;T&gt;.Write(T, TelemetryWriter)</c>, virtual (research R49 revised: converters delegate to the format's writer); its default throws
    /// <see cref="NotSupportedException"/> naming the converter (R45).</item>
    /// <item>The internal <c>ResolveConverter(Type)</c> of the options, through
    /// <see cref="ConverterRoundTrip.BuiltIn{T}"/>, to reach a built-in converter as a property definition
    /// would (R33).</item>
    /// </list>
    /// </remarks>
    public sealed class ConverterWriteTests
    {
        // T002 - US1-1 / SC-001 (quickstart 1): every built-in kind writes the text it reads
        [Theory]
        [InlineData("bool")]
        [InlineData("char")]
        [InlineData("int")]
        [InlineData("long")]
        [InlineData("double")]
        [InlineData("decimal")]
        [InlineData("string")]
        [InlineData("Guid")]
        [InlineData("DateTime")]
        [InlineData("DateTimeOffset")]
        [InlineData("int?")]
        [InlineData("Measure")]
        public void Write_ForEachBuiltInKind_ItShould_ReadBackTheSameValue(string kind)
        {
            // Arrange + Act + Assert
            switch (kind)
            {
                case "bool":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<bool>(), true);
                    break;
                case "char":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<char>(), 'å');
                    break;
                case "int":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<int>(), -2_147_483_648);
                    break;
                case "long":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<long>(), 9_007_199_254_740_993L);
                    break;
                case "double":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<double>(), 0.1 + 0.2);
                    break;
                case "decimal":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<decimal>(), 1234.5678m);
                    break;
                case "string":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<string>(), "Gran; tall \"å\"");
                    break;
                case "Guid":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<Guid>(), new Guid("3cdb35d7-6108-4b19-8d30-d6dfd2c44e9b"));
                    break;
                case "DateTime":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<DateTime>(), new DateTime(2026, 1, 14, 17, 14, 5, 123, DateTimeKind.Utc));
                    break;
                case "DateTimeOffset":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<DateTimeOffset>(), new DateTimeOffset(2026, 1, 14, 17, 14, 5, 123, TimeSpan.FromHours(1)));
                    break;
                case "int?":
                    AssertRoundTrip(ConverterRoundTrip.BuiltIn<int?>(), 42);
                    break;
                case "Measure":
                    AssertRoundTrip(new MeasureConverter(), new Measure(31.4m));
                    break;
                default:
                    Assert.Fail($"No case for kind '{kind}'");
                    break;
            }
        }

        // T003 - US1-2 / FR-002: a value larger than the first offer is written whole, by asking for room
        [Fact]
        public void Write_ForAStringLongerThanTheFirstOffer_ItShould_WriteItWhole()
        {
            // Arrange
            Converter<string> converter = ConverterRoundTrip.BuiltIn<string>();
            string value = new('å', 10_000);
            SmallOfferBufferWriter utf8 = new();

            // Act
            converter.Write(value, PlainTelemetryWriter.Over(utf8));

            // Assert
            Assert.Equal(value, Encoding.UTF8.GetString(utf8.WrittenSpan));
            Assert.Contains(utf8.Hints, hint => hint >= Encoding.UTF8.GetByteCount(value));
        }

        // T004 - US1-3 / FR-004: a converter added to the options is the one that writes
        [Fact]
        public void Write_ForAnInt32ConverterInTheOptions_ItShould_BeTheResolvedConverterThatWrites()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new WritingMarkerInt32Converter());
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();
            Assert.True(options.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(Log.LogKey));

            // Act
            byte[] written = ConverterRoundTrip.Write(Assert.IsType<WritingMarkerInt32Converter>(logKey.ResolvedConverter), 5);

            // Assert
            Assert.Equal("x"u8.ToArray(), written);
        }

        // T005 - R45: a converter that does not override Write says so, naming itself
        [Fact]
        public void Write_ForAConverterThatOnlyReads_ItShould_ThrowNamingTheConverter()
        {
            // Arrange
            Converter<int> converter = new MarkingInt32Converter();

            // Act
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => ConverterRoundTrip.Write(converter, 5));

            // Assert
            Assert.Contains(nameof(MarkingInt32Converter), exception.Message);
        }

        private static void AssertRoundTrip<T>(Converter<T> converter, T value)
        {
            // Act
            T read = ConverterRoundTrip.RoundTrip(converter, value);

            // Assert
            Assert.Equal(value, read);
        }
    }
}
