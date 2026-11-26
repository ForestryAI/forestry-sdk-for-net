using Forestry.Xml;
using Forestry.Xml.Reading;
using Forestry.Serialization.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 1: flatten a document into telemetries of a flat class (tasks.md T010-T020)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until US1's implementation
    /// (T030-T042) declares the surface they assume. For the review gate (T029), the assumed surface:
    /// <list type="bullet">
    /// <item><c>Telemetry</c> in <c>Forestry.Serialization</c>; names in the document declared with <c>XmlElementNameAttribute</c> in <c>Forestry.Serialization.Xml</c> (spec 003 US2, research R37; spec 001 had <c>TelemetryAttribute</c> and <c>FieldAttribute</c> in the core).</item>
    /// <item><c>DeserializeXmlOptions.Telemetries.Add&lt;T&gt;()</c> (R12).</item>
    /// <item><c>TelemetrySerialization.Deserialize(string, DeserializeOptions)</c>, reached through <see cref="EntryPoints"/>.</item>
    /// <item>An unconvertible value throws <see cref="FormatException"/> naming the member, its place (path) and the class (FR-007).</item>
    /// <item>A document cut off inside a telemetry throws the reader's <c>XmlException</c> (spec edge case, R7).</item>
    /// </list>
    /// Telemetries are read with a plain cast; <c>AsType&lt;T&gt;()</c> is US2's.
    /// Every shell needs the XML reader's element and text reading (BLOCKING.md phase 2) to pass.
    /// Each test has its own document, one element per line, so what it reads is next to what it asserts.
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS1
    {
        private static DeserializeXmlOptions OptionsWith<TTelemetry>() where TTelemetry : Telemetry
        {
            DeserializeXmlOptions options = new();
            options.Telemetries.Add<TTelemetry>();
            return options;
        }

        // T010 - quickstart 1 / US1-1 / FR-003
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoLogOccurrences_ItShould_YieldTwoTelemetriesInDocumentOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Log first = (Log)telemetries[0];
            Log second = (Log)telemetries[1];
            Assert.Equal(1, first.LogKey);
            Assert.Equal(0.59m, first.LogVolume);
            Assert.Equal(2, second.LogKey);
            Assert.Equal(0.50m, second.LogVolume);
        }

        // T011 - quickstart 2 / US1-2 / FR-005
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForDataOutsideLogsAndUnknownMembers_ItShould_IgnoreThem(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Header>
                    <LogKey>9</LogKey>
                  </Header>
                  <Log>
                    <LogKey>1</LogKey>
                    <Unknown>x</Unknown>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                  <Footer />
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.Equal(1, ((Log)telemetries[0]).LogKey);
            Assert.Equal(0.59m, ((Log)telemetries[0]).LogVolume);
            Assert.Equal(2, ((Log)telemetries[1]).LogKey);
            Assert.Equal(0.50m, ((Log)telemetries[1]).LogVolume);
        }

        // T012 - quickstart 3 / US1-3
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForARootWithoutLogs_ItShould_YieldNoTelemetriesAndNoError(EntryPoint entryPoint)
        {
            // Arrange + Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, "<Root />", OptionsWith<Log>());

            // Assert
            Assert.Empty(telemetries);
        }

        // T013 - spec edge case: an empty element for a member
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEmptyMemberElement_ItShould_SetNullOrTheDefault(EntryPoint entryPoint)
        {
            // Arrange
            const string notNullableDocument = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume />
                  </Log>
                </Root>
                """;
            const string nullableDocument = """
                <Root>
                  <LogWithNullableVolume>
                    <LogKey>1</LogKey>
                    <LogVolume />
                  </LogWithNullableVolume>
                </Root>
                """;

            // Act
            List<Telemetry> notNullable = EntryPoints.Read(entryPoint, notNullableDocument, OptionsWith<Log>());
            List<Telemetry> nullable = EntryPoints.Read(entryPoint, nullableDocument, OptionsWith<LogWithNullableVolume>());

            // Assert
            Assert.Equal(0m, ((Log)Assert.Single(notNullable)).LogVolume);
            Assert.Null(((LogWithNullableVolume)Assert.Single(nullable)).LogVolume);
        }

        // T014 - quickstart 12 / FR-007
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnUnconvertibleValue_ItShould_ThrowNamingMemberPlaceAndClass(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>abc</LogKey>
                  </Log>
                </Root>
                """;

            // Act
            FormatException exception = Assert.Throws<FormatException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith<Log>()));

            // Assert
            Assert.Contains("LogKey", exception.Message);
            Assert.Contains("Root/Log/LogKey", exception.Message);
            Assert.Contains(nameof(Log), exception.Message);
        }

        // T015 - quickstart 14 / spec edge case: a document cut off inside an open telemetry
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForADocumentCutOffInsideALog_ItShould_ThrowAndNeverHandOutTheOpenTelemetry(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                """;
            List<Telemetry> handedOut = new();

            // Act
            Assert.Throws<XmlException>(() =>
            {
                foreach (Telemetry telemetry in EntryPoints.Read(entryPoint, document, OptionsWith<Log>()))
                {
                    handedOut.Add(telemetry);
                }
            });

            // Assert
            Assert.DoesNotContain(handedOut, telemetry => ((Log)telemetry).LogKey == 2);
        }

        // T016 - flow 2: a public field member is set like a property
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForFieldMembers_ItShould_SetTheFields(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <LogWithFieldMember>
                    <LogKey>1</LogKey>
                    <LogVolume>0.59</LogVolume>
                  </LogWithFieldMember>
                  <LogWithFieldMember>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </LogWithFieldMember>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithFieldMember>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.Equal(1, ((LogWithFieldMember)telemetries[0]).LogKey);
            Assert.Equal(0.59m, ((LogWithFieldMember)telemetries[0]).LogVolume);
            Assert.Equal(0.50m, ((LogWithFieldMember)telemetries[1]).LogVolume);
        }

        // T017 - flow 2: [XmlElementName("Volume")] on a member renames it (spec 001 [Field("Volume")]; spec 003 T018)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForARenamedMember_ItShould_SetItFromTheElementNameOnly(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <Volume>0.59</Volume>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <LogVolume>0.50</LogVolume>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithRenamedMember>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.Equal(0.59m, ((LogWithRenamedMember)telemetries[0]).LogVolume);
            Assert.Equal(0m, ((LogWithRenamedMember)telemetries[1]).LogVolume);
        }

        // T018 - flows 1 and 2: [XmlElementName("Tree")] on a class renames a telemetry (spec 001 [Telemetry("Tree")]; spec 003 T018)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForARenamedTelemetry_ItShould_ReadTheElementNameOnly(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Tree>
                    <LogKey>1</LogKey>
                  </Tree>
                  <Log>
                    <LogKey>2</LogKey>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<TreeLog>());

            // Assert
            Assert.Equal(1, ((TreeLog)Assert.Single(telemetries)).LogKey);
        }

        // T019 - flow 3: the XML default converter for bool accepts 1 and 0 (XML Schema)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForXmlSchemaBooleans_ItShould_ConvertOneAndZero(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <TopSawing>1</TopSawing>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                    <TopSawing>0</TopSawing>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithFlag>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.True(((LogWithFlag)telemetries[0]).TopSawing);
            Assert.False(((LogWithFlag)telemetries[1]).TopSawing);
        }

        // T080 - flow 3: a converter added to the options wins over the XML default (options, format defaults, primitives)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForABooleanConverterInTheOptions_ItShould_BeUsedInsteadOfTheXmlDefault(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                    <TopSawing>1</TopSawing>
                  </Log>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith<LogWithFlag>();
            options.Converters.Add(new MarkingBooleanConverter());

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.False(((LogWithFlag)Assert.Single(telemetries)).TopSawing);
        }

        // T020 - FR-008 / R5 rule 1: exactly one constructor marked [TelemetryConstructor] is used, whatever its accessibility
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForOneMarkedPrivateConstructor_ItShould_ConstructTelemetriesWithIt(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Log>
                    <LogKey>1</LogKey>
                  </Log>
                  <Log>
                    <LogKey>2</LogKey>
                  </Log>
                </Root>
                """;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithMarkedConstructor>());

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.All(telemetries, telemetry => Assert.True(((LogWithMarkedConstructor)telemetry).WasConstructedByMarked()));
            Assert.Equal(1, ((LogWithMarkedConstructor)telemetries[0]).LogKey);
            Assert.Equal(2, ((LogWithMarkedConstructor)telemetries[1]).LogKey);
        }

        // T076 - FR-008 / R8: two telemetry types read from the same element throw at the call, naming both and the element.
        // The options are rejected before any byte is read, so the document is never read.
        [Fact]
        public void Deserialize_ForTwoTelemetryTypesWithTheSameElementName_ItShould_ThrowAtTheCall()
        {
            // Arrange
            DeserializeXmlOptions options = new();
            options.Telemetries.Add<Log>().Add<LogWithRenamedMember>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerialization.Deserialize("<Root />", options));

            // Assert
            Assert.Contains(nameof(Log), exception.Message);
            Assert.Contains(nameof(LogWithRenamedMember), exception.Message);
            Assert.Contains("'Log'", exception.Message);
        }

        // T076 - FR-008 / R8: two members read from the same element throw at the call, naming both, the element and the class;
        // spec 003 T019: and the attribute to change, XmlElementName (US2-4, FR-007).
        // The options are rejected before any byte is read, so the document is never read.
        [Fact]
        public void Deserialize_ForTwoMembersWithTheSameElementName_ItShould_ThrowAtTheCall()
        {
            // Arrange + Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => TelemetrySerialization.Deserialize("<Root />", OptionsWith<LogWithDuplicateMember>()));

            // Assert
            Assert.Contains(nameof(LogWithDuplicateMember), exception.Message);
            Assert.Contains(nameof(LogWithDuplicateMember.Volume), exception.Message);
            Assert.Contains("'LogVolume'", exception.Message);
            Assert.Contains("XmlElementName", exception.Message);  // spec 003 T019, FR-007
        }

        // Spec 003 T022 - research R37: XmlElementName requires a name; without the attribute the class or
        // member name is used, so an empty one has no use
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void XmlElementName_ForAnEmptyName_ItShould_ThrowArgumentException(string? name)
        {
            // Arrange + Act + Assert
            Assert.ThrowsAny<ArgumentException>(() => new XmlElementNameAttribute(name!));
        }
    }
}
