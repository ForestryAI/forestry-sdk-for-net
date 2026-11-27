using Forestry.Serialization.Converters;
using Forestry.Serialization.Definitions;
using Forestry.Serialization.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Tests
{
    /// <summary>
    /// The resolved converter of a property definition (spec 001 tasks.md T028, flow 3, R14; named
    /// "resolved" in spec 003, research R33)
    /// </summary>
    /// <remarks>
    /// Test shell written before any code (C4). For the review gate (T029), the assumed surface:
    /// <c>DeserializeOptions.Converters</c> to add a developer's converter, <c>MakeReadOnly()</c> and the
    /// internal <c>TryGetTypeDefinition</c> (as in <see cref="OptionsTests"/>), and on the type definition its
    /// <c>Properties</c>, each with <c>Name</c> and its <c>ResolvedConverter</c> (spec 003 T009).
    /// </remarks>
    public sealed class ConverterResolutionTests
    {
        // T028 - a converter added to the options wins over the primitive (options, attributes, format defaults, primitives)
        [Fact]
        public void EffectiveConverter_ForAnInt32ConverterInTheOptions_ItShould_BeUsedInsteadOfThePrimitive()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Telemetries.Add<Log>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(Log), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(Log.LogKey));

            // Assert
            Assert.IsType<MarkingInt32Converter>(logKey.ResolvedConverter);
        }

        // T081 - a converter added to the options is used inside Nullable<T>
        [Fact]
        public void EffectiveConverter_ForANullableInt32WithAnInt32ConverterInTheOptions_ItShould_WrapIt()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Converters.Add(new MarkingInt32Converter());
            options.Telemetries.Add<LogWithNullableKey>();
            options.MakeReadOnly();

            // Act
            Assert.True(options.TryGetTypeDefinition(typeof(LogWithNullableKey), out TypeDefinition? definition));
            PropertyDefinition logKey = Assert.Single(definition!.Properties, property => property.Name == nameof(LogWithNullableKey.LogKey));

            // Assert
            Converter<int?> converter = Assert.IsAssignableFrom<Converter<int?>>(logKey.ResolvedConverter);
            Assert.Equal(-1, converter.Read("5"u8));
        }

        /// <summary>
        /// Spec 005 T011: the type definition of <see cref="EnumTelemetry"/> with
        /// <see cref="MemberAwareEnumFactory"/> among the format converters, as a format package registers its
        /// enum converter factory (R64, R65)
        /// </summary>
        private static TypeDefinition EnumTelemetryWithMemberAwareFactory(params Converter[] registered)
        {
            TestingDeserializeOptions options = new() { Format = [new MemberAwareEnumFactory()] };

            foreach (Converter converter in registered)
            {
                options.Converters.Add(converter);
            }

            options.Telemetries.Add<EnumTelemetry>();
            options.MakeReadOnly();
            Assert.True(options.TryGetTypeDefinition(typeof(EnumTelemetry), out TypeDefinition? definition));
            return definition!;
        }

        private static Converter ResolvedConverterOf(TypeDefinition definition, string member) =>
            Assert.Single(definition.Properties, property => property.Name == member).ResolvedConverter;

        // T011 - spec 005 R58 (quickstart 4): two members of one enum type get the converter their member asks for
        [Fact]
        public void ResolvedConverter_ForTwoMembersOfOneEnumTypeWithAMemberAwareFactory_ItShould_DifferPerMember()
        {
            // Arrange + Act
            TypeDefinition definition = EnumTelemetryWithMemberAwareFactory();

            // Assert
            TestEnumConverter<Shade> byName = Assert.IsType<TestEnumConverter<Shade>>(ResolvedConverterOf(definition, nameof(EnumTelemetry.Shade)));
            TestEnumConverter<Shade> byNumber = Assert.IsType<TestEnumConverter<Shade>>(ResolvedConverterOf(definition, nameof(EnumTelemetry.ShadeNumber)));
            Assert.Equal(EnumForm.Name, byName.Form);
            Assert.Equal(EnumForm.Number, byNumber.Form);
        }

        // T011 - spec 005 R58: the nullable factory passes the member on, so the inner converter is the member's
        [Fact]
        public void ResolvedConverter_ForANullableEnumMemberWithAMemberAwareFactory_ItShould_WrapTheMembersConverter()
        {
            // Arrange + Act
            TypeDefinition definition = EnumTelemetryWithMemberAwareFactory();

            // Assert
            Converter<Shade?> converter = Assert.IsAssignableFrom<Converter<Shade?>>(ResolvedConverterOf(definition, nameof(EnumTelemetry.MaybeShadeNumber)));
            Assert.Equal(Shade.Dark, converter.Read("1"u8));
        }

        // T011 - spec 005 R58 / FR-002e: a converter registered for the type still wins over a member-aware format factory
        [Fact]
        public void ResolvedConverter_ForAnEnumWithARegisteredConverterAndAMemberAwareFactory_ItShould_BeTheRegisteredConverter()
        {
            // Arrange
            TestEnumConverter<Shade> registered = new(EnumForm.Name);

            // Act
            TypeDefinition definition = EnumTelemetryWithMemberAwareFactory(registered);

            // Assert
            Assert.Same(registered, ResolvedConverterOf(definition, nameof(EnumTelemetry.Shade)));
            Assert.Same(registered, ResolvedConverterOf(definition, nameof(EnumTelemetry.ShadeNumber)));
        }
    }
}
