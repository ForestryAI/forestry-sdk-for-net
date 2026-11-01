using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 001, User Story 1: shred a flat class into rows (tasks.md T010-T020)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until US1's implementation
    /// (T030-T042) declares the surface they assume. For the review gate (T029), the assumed surface:
    /// <list type="bullet">
    /// <item><c>Row</c>, <c>RowAttribute</c>, <c>FieldAttribute</c> in <c>Forestry.Deserialize</c>.</item>
    /// <item><c>DeserializeXmlOptions.Rows.Add&lt;T&gt;()</c> (R12).</item>
    /// <item><c>Deserialization.Deserialize(string, DeserializeOptions)</c>, reached through <see cref="EntryPoints"/>.</item>
    /// <item>An unconvertible value throws <see cref="FormatException"/> naming the member, its place (path) and the class (FR-007).</item>
    /// <item>A document cut off inside a row throws the reader's <c>XmlException</c> (spec edge case, R7).</item>
    /// </list>
    /// Rows are read with a plain cast; <c>AsType&lt;T&gt;()</c> is US2's. Async rows are skipped until US3 (T066).
    /// Every shell needs the XML reader's element and text reading (BLOCKING.md phase 2) to pass.
    /// </remarks>
    public sealed class WalkingSkeletonTestsUS1
    {
        private const string Document1 =
            "<Root><Log><LogKey>1</LogKey><LogVolume>0.59</LogVolume></Log>" +
            "<Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log></Root>";

        private static DeserializeXmlOptions OptionsWith<TRow>() where TRow : Row
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add<TRow>();
            return options;
        }

        // T010 - quickstart 1 / US1-1 / FR-003
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForTwoLogOccurrences_ItShould_YieldTwoRowsInDocumentOrder(EntryPoint entryPoint)
        {
            List<Row> rows = EntryPoints.Read(entryPoint, Document1, OptionsWith<Log>());

            Assert.Equal(2, rows.Count);
            Log first = (Log)rows[0];
            Log second = (Log)rows[1];
            Assert.Equal(1, first.LogKey);
            Assert.Equal(0.59m, first.LogVolume);
            Assert.Equal(2, second.LogKey);
            Assert.Equal(0.50m, second.LogVolume);
        }

        // T011 - quickstart 2 / US1-2 / FR-005
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForDataOutsideLogsAndUnknownMembers_ItShould_IgnoreThem(EntryPoint entryPoint)
        {
            const string document =
                "<Root><Header><LogKey>9</LogKey></Header>" +
                "<Log><LogKey>1</LogKey><Unknown>x</Unknown><LogVolume>0.59</LogVolume></Log>" +
                "<Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log><Footer /></Root>";

            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());

            Assert.Equal(2, rows.Count);
            Assert.Equal(1, ((Log)rows[0]).LogKey);
            Assert.Equal(0.59m, ((Log)rows[0]).LogVolume);
            Assert.Equal(2, ((Log)rows[1]).LogKey);
            Assert.Equal(0.50m, ((Log)rows[1]).LogVolume);
        }

        // T012 - quickstart 3 / US1-3
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForARootWithoutLogs_ItShould_YieldNoRowsAndNoError(EntryPoint entryPoint)
        {
            List<Row> rows = EntryPoints.Read(entryPoint, "<Root />", OptionsWith<Log>());

            Assert.Empty(rows);
        }

        // T013 - spec edge case: an empty element for a member
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForAnEmptyMemberElement_ItShould_SetNullOrTheDefault(EntryPoint entryPoint)
        {
            const string document = "<Root><Log><LogKey>1</LogKey><LogVolume /></Log></Root>";

            List<Row> notNullable = EntryPoints.Read(entryPoint, document, OptionsWith<Log>());
            List<Row> nullable = EntryPoints.Read(entryPoint, document.Replace("Log>", "LogWithNullableVolume>"), OptionsWith<LogWithNullableVolume>());

            Assert.Equal(0m, ((Log)Assert.Single(notNullable)).LogVolume);
            Assert.Null(((LogWithNullableVolume)Assert.Single(nullable)).LogVolume);
        }

        // T014 - quickstart 12 / FR-007
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForAnUnconvertibleValue_ItShould_ThrowNamingMemberPlaceAndClass(EntryPoint entryPoint)
        {
            const string document = "<Root><Log><LogKey>abc</LogKey></Log></Root>";

            FormatException exception = Assert.Throws<FormatException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith<Log>()));

            Assert.Contains("LogKey", exception.Message);
            Assert.Contains("Root/Log/LogKey", exception.Message);
            Assert.Contains(nameof(Log), exception.Message);
        }

        // T015 - quickstart 14 / spec edge case: a document cut off inside an open row
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForADocumentCutOffInsideALog_ItShould_ThrowAndNeverHandOutTheOpenRow(EntryPoint entryPoint)
        {
            const string document = "<Root><Log><LogKey>1</LogKey><LogVolume>0.59</LogVolume></Log><Log><LogKey>2</LogKey>";
            List<Row> handedOut = new();

            Assert.Throws<XmlException>(() =>
            {
                foreach (Row row in EntryPoints.Read(entryPoint, document, OptionsWith<Log>()))
                {
                    handedOut.Add(row);
                }
            });

            Assert.DoesNotContain(handedOut, row => ((Log)row).LogKey == 2);
        }

        // T016 - flow 2: a public field member is set like a property
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForFieldMembers_ItShould_SetTheFields(EntryPoint entryPoint)
        {
            string document = Document1.Replace("Log>", "LogWithFieldMember>");

            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithFieldMember>());

            Assert.Equal(2, rows.Count);
            Assert.Equal(1, ((LogWithFieldMember)rows[0]).LogKey);
            Assert.Equal(0.59m, ((LogWithFieldMember)rows[0]).LogVolume);
            Assert.Equal(0.50m, ((LogWithFieldMember)rows[1]).LogVolume);
        }

        // T017 - flow 2: [Field("Volume")] renames a member
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForARenamedField_ItShould_SetItFromTheMediaNameOnly(EntryPoint entryPoint)
        {
            const string document =
                "<Root><Log><LogKey>1</LogKey><Volume>0.59</Volume></Log>" +
                "<Log><LogKey>2</LogKey><LogVolume>0.50</LogVolume></Log></Root>";

            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithRenamedField>());

            Assert.Equal(2, rows.Count);
            Assert.Equal(0.59m, ((LogWithRenamedField)rows[0]).LogVolume);
            Assert.Equal(0m, ((LogWithRenamedField)rows[1]).LogVolume);
        }

        // T018 - flows 1 and 2: [Row("Tree")] renames a row
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForARenamedRow_ItShould_ShredTheMediaNameOnly(EntryPoint entryPoint)
        {
            const string document = "<Root><Tree><LogKey>1</LogKey></Tree><Log><LogKey>2</LogKey></Log></Root>";

            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<TreeLog>());

            Assert.Equal(1, ((TreeLog)Assert.Single(rows)).LogKey);
        }

        // T019 - flow 3: the XML default converter for bool accepts 1 and 0 (XML Schema)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForXmlSchemaBooleans_ItShould_ConvertOneAndZero(EntryPoint entryPoint)
        {
            const string document =
                "<Root><Log><LogKey>1</LogKey><TopSawing>1</TopSawing></Log>" +
                "<Log><LogKey>2</LogKey><TopSawing>0</TopSawing></Log></Root>";

            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith<LogWithFlag>());

            Assert.Equal(2, rows.Count);
            Assert.True(((LogWithFlag)rows[0]).TopSawing);
            Assert.False(((LogWithFlag)rows[1]).TopSawing);
        }

        // T020 - FR-008 / R5 rule 1: exactly one constructor marked [RowConstructor] is used, whatever its accessibility
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async, Skip = "US3 (T066)")]
        public void Deserialize_ForOneMarkedPrivateConstructor_ItShould_ConstructRowsWithIt(EntryPoint entryPoint)
        {
            List<Row> rows = EntryPoints.Read(entryPoint, Document1, OptionsWith<LogWithMarkedConstructor>());

            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.True(((LogWithMarkedConstructor)row).WasConstructedByMarked()));
            Assert.Equal(1, ((LogWithMarkedConstructor)rows[0]).LogKey);
            Assert.Equal(2, ((LogWithMarkedConstructor)rows[1]).LogKey);
        }
    }
}
