using System.Text;
using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// Spec 005 US1: the base enum converter, its forms and declared member names (tasks.md T007-T010;
    /// research R59, R62)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4); they compile once US1's implementation (T014-T022) declares
    /// the surface they assume. For the review gate (T013), the assumed surface:
    /// <list type="bullet">
    /// <item><c>EnumForm { Name, Number }</c> and the public abstract <c>EnumConverter&lt;TEnum&gt;</c> with a
    /// protected constructor taking the form (R59); <see cref="TestEnumConverter{TEnum}"/> is a test subclass.</item>
    /// <item><c>[EnumMemberName("…")]</c> on enum fields (R62); a name clash ignoring case fails at lock with an
    /// <see cref="InvalidOperationException"/> naming the enum and the name.</item>
    /// <item>The built-in enum converter is in name form; names read ignoring case; the form is never guessed
    /// from the text (FR-002).</item>
    /// <item>In name form, an undefined value is refused when written with a <see cref="NotSupportedException"/>
    /// naming the value and the enum (R59, confirmed 2026-11-27).</item>
    /// </list>
    /// </remarks>
    public sealed class EnumConverterTests
    {
        private static string Written<T>(Converter<T> converter, T value) => Encoding.UTF8.GetString(ConverterRoundTrip.Write(converter, value));

        // T007 - US1-5 / FR-005: a converter registered for an enum type wins over the built-in enum converter
        [Fact]
        public void ResolvedConverter_ForAnEnumWithAConverterRegistered_ItShould_BeTheRegisteredConverter()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            TestEnumConverter<Shade> registered = new(EnumForm.Number);
            options.Converters.Add(registered);
            options.Telemetries.Add<EnumTelemetry>();

            // Act
            options.MakeReadOnly();

            // Assert
            Assert.True(options.TryGetTypeDefinition(typeof(EnumTelemetry), out TypeDefinition? definition));
            PropertyDefinition shade = Assert.Single(definition!.Properties, property => property.Name == nameof(EnumTelemetry.Shade));
            Assert.Same(registered, shade.ResolvedConverter);
        }

        // T008 - US1-6 / FR-002: the built-in converter writes and reads names, ignoring case when reading
        [Fact]
        public void BuiltInEnumConverter_ForAMember_ItShould_WriteItsNameAndReadItIgnoringCase()
        {
            // Arrange
            Converter<Shade> converter = ConverterRoundTrip.BuiltIn<Shade>();

            // Act
            string written = Written(converter, Shade.Dark);
            Shade read = converter.Read("dark"u8);

            // Assert
            Assert.Equal("Dark", written);
            Assert.Equal(Shade.Dark, read);
        }

        // T008 - FR-002: the built-in converter is in name form, so a number is not guessed to be one
        [Fact]
        public void BuiltInEnumConverter_ForANumber_ItShould_ThrowAFormatException()
        {
            // Arrange
            Converter<Shade> converter = ConverterRoundTrip.BuiltIn<Shade>();

            // Act + Assert
            Assert.Throws<FormatException>(() => converter.Read("1"u8));
        }

        // T008 - US1-6: a converter in number form writes and reads the number, and a name is not guessed to be one
        [Fact]
        public void EnumConverter_InNumberForm_ItShould_WriteAndReadTheNumberOnly()
        {
            // Arrange
            TestEnumConverter<Shade> converter = new(EnumForm.Number);

            // Act
            string written = Written(converter, Shade.Dark);
            Shade read = converter.Read("1"u8);

            // Assert
            Assert.Equal(EnumForm.Number, converter.Form);
            Assert.Equal("1", written);
            Assert.Equal(Shade.Dark, read);
            Assert.Throws<FormatException>(() => converter.Read("Dark"u8));
        }

        // T009 - FR-002c / R62 (quickstart 3): a declared member name is used both ways
        [Fact]
        public void EnumConverter_ForADeclaredMemberName_ItShould_WriteItAndReadItIgnoringCase()
        {
            // Arrange
            Converter<Quality> converter = ConverterRoundTrip.BuiltIn<Quality>();

            // Act
            string written = Written(converter, Quality.A);
            Quality read = converter.Read("quality a"u8);

            // Assert
            Assert.Equal("Quality A", written);
            Assert.Equal(Quality.A, read);
        }

        // T009 - R62 / S1: declared names that clash ignoring case fail at lock, naming the enum and the name
        [Fact]
        public void MakeReadOnly_ForAnEnumWithClashingMemberNames_ItShould_ThrowNamingTheEnumAndTheName()
        {
            // Arrange
            TestingSerializeOptions options = new();
            options.Telemetries.Add<ClashTelemetry>();

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.MakeReadOnly);

            // Assert
            Assert.Contains(nameof(Clash), exception.Message, StringComparison.Ordinal);
            Assert.Contains("X", exception.Message, StringComparison.Ordinal);
        }

        // T010 - R59 (quickstart 3): an undefined value cannot be written by name
        [Fact]
        public void EnumConverter_InNameFormForAnUndefinedValue_ItShould_RefuseNamingTheValueAndTheEnum()
        {
            // Arrange
            Converter<Shade> converter = ConverterRoundTrip.BuiltIn<Shade>();

            // Act
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => Written(converter, (Shade)7));

            // Assert
            Assert.Contains("7", exception.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(Shade), exception.Message, StringComparison.Ordinal);
        }

        // T010 - R59: a flags combination is written and read as its names joined with ", "
        [Fact]
        public void EnumConverter_ForAFlagsCombination_ItShould_WriteAndReadTheJoinedNames()
        {
            // Arrange
            Converter<Parts> converter = ConverterRoundTrip.BuiltIn<Parts>();

            // Act
            string written = Written(converter, Parts.Top | Parts.Butt);
            Parts read = converter.Read("Top, Butt"u8);

            // Assert
            Assert.Equal("Top, Butt", written);
            Assert.Equal(Parts.Top | Parts.Butt, read);
        }

        // T010 - R59: a ulong above long.MaxValue is written exactly in number form and reads back
        [Fact]
        public void EnumConverter_InNumberFormForAnUnsignedValueAboveLongMax_ItShould_WriteAndReadItExactly()
        {
            // Arrange
            TestEnumConverter<Wide> converter = new(EnumForm.Number);

            // Act
            string written = Written(converter, Wide.Max);
            Wide read = converter.Read(Encoding.UTF8.GetBytes(written));

            // Assert
            Assert.Equal("18446744073709551615", written);
            Assert.Equal(Wide.Max, read);
        }
    }
}
