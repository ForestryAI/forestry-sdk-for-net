using Forestry.Serialization.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Xml.Tests
{
    /// <summary>
    /// Serialization spec 005, User Story 4 (#152): XML documents read enums and durations as XML writes
    /// them (tasks.md T059-T062, quickstart 6)
    /// </summary>
    /// <remarks>
    /// Test shells written before the review gate (C4). They compile once US4 adds
    /// <see cref="XmlEnumFormAttribute"/>; the number form and the durations fail until the XML enum and
    /// duration converters are registered in <see cref="DeserializeXmlOptions"/>.
    /// </remarks>
    public sealed class XmlEnumAndDurationTests
    {
        private static DeserializeXmlOptions Options()
        {
            DeserializeXmlOptions options = new();
            options.Telemetries.Add<EnumAndDurationTelemetry>();
            return options;
        }

        private static string Document(string member, string text) => $"""
            <Root>
              <Log>
                <LogKey>1</LogKey>
                <{member}>{text}</{member}>
              </Log>
            </Root>
            """;

        private static EnumAndDurationTelemetry ReadOne(EntryPoint entryPoint, string member, string text, DeserializeXmlOptions? options = null) =>
            (EnumAndDurationTelemetry)Assert.Single(EntryPoints.Read(entryPoint, Document(member, text), options ?? Options()));

        // T059 - US4-1: without the attribute, a name (ignoring case) reads; a number is refused
        [Theory]
        [InlineData(EntryPoint.Sync, "Dark")]
        [InlineData(EntryPoint.Sync, "dark")]
        [InlineData(EntryPoint.Async, "DARK")]
        public void Deserialize_ForAnEnumName_ItShould_ReadTheMember(EntryPoint entryPoint, string text)
        {
            // Act
            EnumAndDurationTelemetry telemetry = ReadOne(entryPoint, "Shade", text);

            // Assert
            Assert.Equal(Shade.Dark, telemetry.Shade);
        }

        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEnumNumberWithoutTheAttribute_ItShould_ThrowFormatException(EntryPoint entryPoint)
        {
            // Act
            FormatException exception = Assert.Throws<FormatException>(() => ReadOne(entryPoint, "Shade", "1"));

            // Assert
            Assert.Contains(nameof(EnumAndDurationTelemetry.Shade), exception.Message);
        }

        // T060 - US4-2: with [XmlEnumForm(EnumForm.Number)], a number reads; a name is refused
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEnumNumberWithTheAttribute_ItShould_ReadTheMember(EntryPoint entryPoint)
        {
            // Act
            EnumAndDurationTelemetry telemetry = ReadOne(entryPoint, "ShadeNumber", "1");

            // Assert
            Assert.Equal(Shade.Dark, telemetry.ShadeNumber);
        }

        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEnumNameWithTheNumberAttribute_ItShould_ThrowFormatException(EntryPoint entryPoint)
        {
            // Act
            FormatException exception = Assert.Throws<FormatException>(() => ReadOne(entryPoint, "ShadeNumber", "Dark"));

            // Assert
            Assert.Contains(nameof(EnumAndDurationTelemetry.ShadeNumber), exception.Message);
        }

        // T061 - US4-3 / FR-002e: the developer's converter for the enum type wins, for both members
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEnumConverterInTheOptions_ItShould_BeUsedInsteadOfTheXmlEnumConverter(EntryPoint entryPoint)
        {
            // Arrange
            DeserializeXmlOptions options = Options();
            options.Converters.Add(new MarkingShadeConverter());

            // Act
            EnumAndDurationTelemetry named = ReadOne(entryPoint, "Shade", "Dark", options);
            EnumAndDurationTelemetry numbered = ReadOne(entryPoint, "ShadeNumber", "1", options);

            // Assert
            Assert.Equal(Shade.Light, named.Shade);
            Assert.Equal(Shade.Light, numbered.ShadeNumber);
        }

        // T062 - US4-4 / FR-003a: XML Schema durations read to the exact TimeSpan
        public static TheoryData<string, TimeSpan> Durations => new()
        {
            { "PT1H30M", new TimeSpan(1, 30, 0) },
            { "P2DT3H", new TimeSpan(2, 3, 0, 0) },
            { "-PT0.5S", TimeSpan.FromMilliseconds(-500) },
            { "PT0S", TimeSpan.Zero },
            { "P1D", TimeSpan.FromDays(1) },
            { "PT1.0000001S", TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1) }
        };

        [Theory]
        [MemberData(nameof(Durations))]
        public void Deserialize_ForAnXmlSchemaDuration_ItShould_ReadTheExactTimeSpan(string text, TimeSpan expected)
        {
            // Act
            EnumAndDurationTelemetry sync = ReadOne(EntryPoint.Sync, "Duration", text);
            EnumAndDurationTelemetry async = ReadOne(EntryPoint.Async, "Duration", text);

            // Assert
            Assert.Equal(expected, sync.Duration);
            Assert.Equal(expected, async.Duration);
        }

        // T062 - years and months have no exact TimeSpan; an empty duration, a T without a part and a
        // text without P are not durations
        [Theory]
        [InlineData("P1M")]
        [InlineData("P1Y")]
        [InlineData("P1Y2M3DT4H")]
        [InlineData("P")]
        [InlineData("PT")]
        [InlineData("1H")]
        [InlineData("PT1H30")]
        public void Deserialize_ForATextThatIsNotAnExactDuration_ItShould_ThrowFormatException(string text)
        {
            // Act
            FormatException exception = Assert.Throws<FormatException>(() => ReadOne(EntryPoint.Sync, "Duration", text));

            // Assert
            Assert.Contains(nameof(EnumAndDurationTelemetry.Duration), exception.Message);
        }
    }
}
