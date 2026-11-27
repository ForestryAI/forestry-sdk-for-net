using System.Text;
using Forestry.StanForD.Converters;
using Forestry.StanForD.Tests.Fixtures;
using Xunit;

namespace Forestry.StanForD.Tests
{
    /// <summary>
    /// Serialization spec 005, User Story 2 (#150): StanForD's enums are written as StanForD texts
    /// (tasks.md T025-T028; research R63)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until US2's implementation declares
    /// the surface they assume. For the review gate (T029), the assumed surface, in namespace
    /// <c>Forestry.StanForD</c>:
    /// <list type="bullet">
    /// <item><c>TOptions AddHarvesting&lt;TOptions&gt;(this TOptions options) where TOptions : SerializeOptions</c>:
    /// adds <see cref="Stem"/> and <see cref="Log"/> and StanForD's enum converters to any format's serialize
    /// options.</item>
    /// <item><see cref="CuttingReason"/> declares its texts with <c>[EnumMemberName]</c> where they differ from the
    /// C# names ("Grade break", "Pulpwood grade", "Top break", "Other manual").</item>
    /// <item><c>StanForDEnumConverter&lt;TEnum&gt;</c> builds on <c>EnumConverter&lt;TEnum&gt;</c> in name form, still
    /// reads texts without spaces or in another case, and writes the declared text.</item>
    /// </list>
    /// Writing goes through <see cref="RecordingSerializeOptions"/>, a minimal format joining values with
    /// <c>;</c>, since a core writer cannot be set up outside a serializer call.
    /// </remarks>
    public sealed class StanForDEnumTests
    {
        /// <summary>
        /// The texts each member is written as, in the order of <see cref="StanForDEnums"/>' members
        /// </summary>
        private static string[] Written(StanForDEnums telemetry)
        {
            RecordingSerializeOptions options = new RecordingSerializeOptions().AddHarvesting();
            options.Telemetries.Add<StanForDEnums>();
            return options.Line(telemetry).Split(';');
        }

        public static TheoryData<CuttingReason, string> CuttingReasons => new()
        {
            { CuttingReason.Automatic, "Automatic" },
            { CuttingReason.Decay, "Decay" },
            { CuttingReason.Damage, "Damage" },
            { CuttingReason.Crook, "Crook" },
            { CuttingReason.Sweep, "Sweep" },
            { CuttingReason.GradeBreak, "Grade break" },
            { CuttingReason.PulpwoodGrade, "Pulpwood grade" },
            { CuttingReason.TopBreak, "Top break" },
            { CuttingReason.Spinning, "Spinning" },
            { CuttingReason.OtherManual, "Other manual" },
        };

        public static TheoryData<ProcessingCategory, string> ProcessingCategories => new()
        {
            { ProcessingCategory.SingleTreeProcessing, "SingleTreeProcessing" },
            { ProcessingCategory.MultiTreeProcessing, "MultiTreeProcessing" },
            { ProcessingCategory.SingleTreeFelling, "SingleTreeFelling" },
            { ProcessingCategory.MultiTreeFelling, "MultiTreeFelling" },
        };

        /// <summary>
        /// T025, US2-1, SC-003: every cutting reason is written as its StanForD text and reads back
        /// </summary>
        [Theory]
        [MemberData(nameof(CuttingReasons))]
        public void CuttingReason_ItShould_BeWrittenAsItsStanForDTextAndReadBack(CuttingReason reason, string text)
        {
            // Arrange
            StanForDEnumConverter<CuttingReason> converter = new();

            // Act
            string written = Written(new StanForDEnums { CuttingReason = reason })[0];

            // Assert
            Assert.Equal(text, written);
            Assert.Equal(reason, converter.Read(Encoding.UTF8.GetBytes(written)));
        }

        /// <summary>
        /// T025, US2-1, SC-003: every processing category is written as its StanForD text (its C# name) and reads back
        /// </summary>
        [Theory]
        [MemberData(nameof(ProcessingCategories))]
        public void ProcessingCategory_ItShould_BeWrittenAsItsStanForDTextAndReadBack(ProcessingCategory category, string text)
        {
            // Arrange
            StanForDEnumConverter<ProcessingCategory> converter = new();

            // Act
            string written = Written(new StanForDEnums { ProcessingCategory = category })[1];

            // Assert
            Assert.Equal(text, written);
            Assert.Equal(category, converter.Read(Encoding.UTF8.GetBytes(written)));
        }

        /// <summary>
        /// T025, US2-1, SC-003: WGS84 is written as WGS84 and reads back
        /// </summary>
        [Fact]
        public void CoordinateReferenceSystem_ItShould_BeWrittenAsWGS84AndReadBack()
        {
            // Arrange
            StanForDEnumConverter<CoordinateReferenceSystem> converter = new();

            // Act
            string written = Written(new StanForDEnums { CoordinateReferenceSystem = CoordinateReferenceSystem.WGS84 })[2];

            // Assert
            Assert.Equal("WGS84", written);
            Assert.Equal(CoordinateReferenceSystem.WGS84, converter.Read(Encoding.UTF8.GetBytes(written)));
        }

        /// <summary>
        /// T026, US2-2, FR-007: serialize options with the Harvesting telemetries lock, with Stem and Log added and
        /// no member refused for lacking a write side
        /// </summary>
        [Fact]
        public void AddHarvesting_ForSerializeOptions_ItShould_AddStemAndLogAndLock()
        {
            // Arrange
            RecordingSerializeOptions options = new();

            // Act
            options.AddHarvesting();
            Exception? failure = Record.Exception(options.MakeReadOnly);

            // Assert
            Assert.Null(failure);
            Assert.Contains(typeof(Stem), options.Telemetries);
            Assert.Contains(typeof(Log), options.Telemetries);
        }

        /// <summary>
        /// T027, US2-3: a StanForD text still reads without its spaces or in another case, as before
        /// </summary>
        [Theory]
        [InlineData("Other manual")]
        [InlineData("other manual")]
        [InlineData("OtherManual")]
        [InlineData("othermanual")]
        public void Read_ForATextWithOrWithoutSpacesInAnyCase_ItShould_ReadTheMember(string text)
        {
            // Arrange
            StanForDEnumConverter<CuttingReason> converter = new();

            // Act
            CuttingReason read = converter.Read(Encoding.UTF8.GetBytes(text));

            // Assert
            Assert.Equal(CuttingReason.OtherManual, read);
        }

        /// <summary>
        /// T027, US2-3: a text naming no member fails, as before
        /// </summary>
        [Fact]
        public void Read_ForAnUnknownText_ItShould_ThrowAFormatException()
        {
            // Arrange
            StanForDEnumConverter<CuttingReason> converter = new();

            // Act + Assert
            Assert.Throws<FormatException>(() => converter.Read("Unknown"u8));
        }

        /// <summary>
        /// T028, US2-4, FR-002e: a format asking for numbers does not change StanForD's enums: its registered
        /// converter wins and writes the StanForD text
        /// </summary>
        [Fact]
        public void Write_ForAFormatAskingForNumbers_ItShould_StillWriteTheStanForDText()
        {
            // Arrange
            RecordingSerializeOptions options = new RecordingSerializeOptions { Format = [new NumberEnumFactory()] }.AddHarvesting();
            options.Telemetries.Add<StanForDEnums>();

            // Act
            string line = options.Line(new StanForDEnums { CuttingReason = CuttingReason.OtherManual });

            // Assert
            Assert.Equal("Other manual;SingleTreeProcessing;WGS84", line);
        }
    }
}
